using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Application.Common;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Domain;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Identity.Infrastructure;

internal sealed class IdentityAccountGateway(UserManager<IdentityUser> users, DbContext context) : IUserAccountGateway
{
    public async Task<AccountCreation> CreateRequesterAsync(string email, string password, string? fullName, CancellationToken cancellationToken)
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
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            var claim = await users.AddClaimAsync(user, new Claim(ClaimTypes.Name, fullName.Trim()));
            if (!claim.Succeeded) return new(null, claim.Errors.Select(e => e.Description).ToArray());
        }
        var result = await ToUserAsync(user);
        await transaction.CommitAsync(cancellationToken);
        return new(result, []);
    }

    public async Task<PasswordSignIn?> SignInAsync(string email, string password, DateTime now, CancellationToken cancellationToken)
    {
        string? rawRefreshToken = null;
        // Lưu refresh token trong cùng giao dịch đang giữ khóa dòng người dùng: đổi/đặt lại mật khẩu hoặc đổi vai trò
        // chạy song song phải chờ, rồi thu hồi luôn token này; không còn khe giữa kiểm mật khẩu và lưu token.
        var user = await VerifyPasswordAsync(email, password, async verified =>
        {
            var (token, raw) = RefreshTokenIssuer.Issue(verified.Id, now);
            context.Set<RefreshToken>().Add(token);
            await context.SaveChangesAsync(cancellationToken);
            rawRefreshToken = raw;
        }, cancellationToken);
        return user is null || rawRefreshToken is null ? null : new PasswordSignIn(user, rawRefreshToken);
    }

    private async Task<UserInfo?> VerifyPasswordAsync(string email, string password, Func<IdentityUser, Task> onVerified,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password)) return null;
        await using var transaction = await context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        // Read committed + row lock: đảm bảo mọi replica quan sát trạng thái lockout mới nhất sau khi chờ.
        // Đọc snapshot mới mà không thay thế instance user đã được theo dõi trong scope này.
        var normalizedEmail = users.NormalizeEmail(email);
        var user = await context.Set<IdentityUser>().FromSqlInterpolated(
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
        await onVerified(user);
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

    public async Task<IReadOnlyList<UserInfo>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var roleUsers = await users.GetUsersInRoleAsync(roleName);
        if (roleUsers.Count == 0) return [];

        // Hai truy vấn gom cho cả danh sách thay vì 2 truy vấn cho mỗi người dùng.
        var ids = roleUsers.Select(u => u.Id).ToArray();
        var names = (await context.Set<IdentityUserClaim<string>>().AsNoTracking()
                .Where(c => ids.Contains(c.UserId) && c.ClaimType == ClaimTypes.Name)
                .Select(c => new { c.UserId, c.ClaimValue })
                .ToListAsync(cancellationToken))
            .GroupBy(c => c.UserId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().ClaimValue, StringComparer.Ordinal);
        var roles = (await (from userRole in context.Set<IdentityUserRole<string>>().AsNoTracking()
                            join role in context.Set<IdentityRole>().AsNoTracking() on userRole.RoleId equals role.Id
                            where ids.Contains(userRole.UserId) && role.Name != null
                            select new { userRole.UserId, RoleName = role.Name! })
                .ToListAsync(cancellationToken))
            .ToLookup(r => r.UserId, r => r.RoleName, StringComparer.Ordinal);

        return roleUsers.Select(user => new UserInfo
        {
            UserId = user.Id, Email = user.Email!, FullName = names.GetValueOrDefault(user.Id), Roles = roles[user.Id].ToArray()
        }).ToArray();
    }

    public async Task<IdentityOperationResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword,
        DateTime revokeSessionsAt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId);
        if (user is null)
        {
            return IdentityOperationResult.Failed("User not found.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await UserSessionLock.LockAsync(context, user.Id, cancellationToken);
        var result = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            return IdentityOperationResult.Failed(result.Errors.Select(e => e.Description));
        }

        await RevokeSessionsAsync(user.Id, revokeSessionsAt, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return IdentityOperationResult.Success();
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return !string.IsNullOrWhiteSpace(email) && await users.FindByEmailAsync(email) is not null;
    }

    public async Task<IdentityOperationResult> AssignRolesAsync(string userId, IReadOnlyList<string> roles, DateTime revokeSessionsAt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId);
        if (user is null)
        {
            return IdentityOperationResult.Failed("User not found.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        // Khóa dòng vai trò Admin: mọi lần gán vai trò chờ nhau tại đây, nên phép đếm Admin bên dưới luôn thấy
        // kết quả của lần trước và hai yêu cầu đồng thời không thể cùng gỡ hai Admin cuối cùng.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM public.\"AspNetRoles\" WHERE \"NormalizedName\" = {RoleNames.Admin.ToUpperInvariant()} FOR UPDATE",
            cancellationToken);
        await UserSessionLock.LockAsync(context, user.Id, cancellationToken);
        var currentRoles = await users.GetRolesAsync(user);
        if (currentRoles.Contains(RoleNames.Admin, StringComparer.OrdinalIgnoreCase) &&
            !roles.Contains(RoleNames.Admin, StringComparer.OrdinalIgnoreCase) &&
            (await users.GetUsersInRoleAsync(RoleNames.Admin)).Count <= 1)
        {
            return IdentityOperationResult.Failed("Không thể gỡ vai trò Admin của quản trị viên cuối cùng.");
        }

        var toRemove = currentRoles.Except(roles, StringComparer.OrdinalIgnoreCase).ToList();
        var toAdd = roles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToList();

        if (toRemove.Count > 0)
        {
            var removeResult = await users.RemoveFromRolesAsync(user, toRemove);
            if (!removeResult.Succeeded)
            {
                return IdentityOperationResult.Failed(removeResult.Errors.Select(e => e.Description));
            }
        }

        if (toAdd.Count > 0)
        {
            var addResult = await users.AddToRolesAsync(user, toAdd);
            if (!addResult.Succeeded)
            {
                return IdentityOperationResult.Failed(addResult.Errors.Select(e => e.Description));
            }
        }

        // Đổi security stamp để mọi cookie/token dựa trên stamp cũ cũng mất hiệu lực, giống đổi mật khẩu.
        EnsureSecurityWrite(await users.UpdateSecurityStampAsync(user));
        await RevokeSessionsAsync(user.Id, revokeSessionsAt, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return IdentityOperationResult.Success();
    }

    /// <summary>Thu hồi mọi refresh token còn hiệu lực của người dùng trong giao dịch hiện tại.</summary>
    private Task<int> RevokeSessionsAsync(string userId, DateTime revokedAt, CancellationToken cancellationToken) =>
        context.Set<RefreshToken>()
            .Where(r => r.UserId == userId && r.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.RevokedAt, revokedAt), cancellationToken);

    private async Task<UserInfo> ToUserAsync(IdentityUser user) => new()
    {
        UserId = user.Id, Email = user.Email!, FullName = (await users.GetClaimsAsync(user)).FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value,
        Roles = (await users.GetRolesAsync(user)).ToArray()
    };
}
