using Microsoft.AspNetCore.Identity;
using ServiceDesk.Application.Identity.Interfaces;
using ServiceDesk.Application.Identity.Models;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Infrastructure.Persistence;

namespace ServiceDesk.Infrastructure.Identity;

public sealed class IdentityAccountGateway(UserManager<IdentityUser> users, ApplicationDbContext context) : IUserAccountGateway
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
        var user = await users.FindByEmailAsync(email);
        if (user is null || await users.IsLockedOutAsync(user)) return null;
        if (!await users.CheckPasswordAsync(user, password))
        {
            var failed = await users.AccessFailedAsync(user);
            if (!failed.Succeeded) context.ChangeTracker.Clear();
            return null;
        }
        var reset = await users.ResetAccessFailedCountAsync(user);
        if (!reset.Succeeded || await users.IsLockedOutAsync(user)) return null;
        return await ToUserAsync(user);
    }

    public async Task<UserInfo?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId);
        return user is null ? null : await ToUserAsync(user);
    }

    private async Task<UserInfo> ToUserAsync(IdentityUser user) => new()
    {
        UserId = user.Id, Email = user.Email!, Roles = (await users.GetRolesAsync(user)).ToArray()
    };
}
