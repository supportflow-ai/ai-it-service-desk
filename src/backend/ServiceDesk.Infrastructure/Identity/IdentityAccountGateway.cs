using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Identity.Ports;
using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Infrastructure.Persistence;

namespace ServiceDesk.Infrastructure.Identity;

internal sealed class IdentityAccountGateway(UserManager<IdentityUser> users, ApplicationDbContext context) : IUserAccountGateway
{
    public async Task<AccountCreation> CreateRequesterAsync(string email, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            return new(null, ["Email and password are required."]);
        if (await users.FindByEmailAsync(email) is not null)
            return new(null, ["Email is already registered."]);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded) return new(null, created.Errors.Select(e => e.Description).ToArray());
        var assigned = await users.AddToRoleAsync(user, RoleNames.Requester);
        if (!assigned.Succeeded) return new(null, assigned.Errors.Select(e => e.Description).ToArray());
        var result = await ToUserAsync(user);
        await transaction.CommitAsync(cancellationToken);
        return new(result, []);
    }

    public async Task<UserInfo?> CheckPasswordAsync(string email, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password)) return null;
        await using var transaction = await context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        // Read committed + row lock: đảm bảo mọi replica quan sát trạng thái lockout mới nhất sau khi chờ.
        // Đọc snapshot mới mà không thay thế instance user đã được theo dõi trong scope này.
        var normalizedEmail = users.NormalizeEmail(email);
        var user = await context.Users.FromSqlInterpolated(
            $"SELECT * FROM public.\"AspNetUsers\" WHERE \"NormalizedEmail\" = {normalizedEmail} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (user is not null)
        {
            var tracked = context.ChangeTracker.Entries<IdentityUser>().SingleOrDefault(e => e.Entity.Id == user.Id);
            if (tracked is not null)
            {
                tracked.CurrentValues.SetValues(user);
                tracked.OriginalValues.SetValues(user);
                tracked.State = EntityState.Unchanged;
                user = tracked.Entity;
            }
            else context.Attach(user);
        }
        if (user is null || await users.IsLockedOutAsync(user)) return null;
        if (!await users.CheckPasswordAsync(user, password))
        {
            var failed = await users.AccessFailedAsync(user);
            EnsureSecurityWrite(failed);
            cancellationToken.ThrowIfCancellationRequested();
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        var reset = await users.ResetAccessFailedCountAsync(user);
        EnsureSecurityWrite(reset);
        if (await users.IsLockedOutAsync(user)) return null;
        var result = await ToUserAsync(user);
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static void EnsureSecurityWrite(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException("identity_security_write_failed");
    }

    public async Task<UserInfo?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId);
        return user is null ? null : await ToUserAsync(user);
    }

    public async Task<bool> IsAgentAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return false;
        return await users.IsInRoleAsync(user, RoleNames.Agent);
    }

    public async Task<string> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return "Người dùng";
        return !string.IsNullOrWhiteSpace(user.UserName) ? user.UserName : (!string.IsNullOrWhiteSpace(user.Email) ? user.Email : "Người dùng");
    }

    public async Task<IReadOnlyList<UserInfo>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var roleUsers = await users.GetUsersInRoleAsync(roleName);
        var result = new List<UserInfo>(roleUsers.Count);
        foreach (var user in roleUsers)
        {
            result.Add(await ToUserAsync(user));
        }
        return result;
    }

    private async Task<UserInfo> ToUserAsync(IdentityUser user) => new()
    {
        UserId = user.Id, Email = user.Email!, Roles = (await users.GetRolesAsync(user)).ToArray()
    };
}
