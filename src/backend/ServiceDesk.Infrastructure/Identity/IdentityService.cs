using Microsoft.AspNetCore.Identity;
using ServiceDesk.Application.Auth;
using ServiceDesk.Application.Common.Exceptions;
using ServiceDesk.Application.Common.Interfaces;

namespace ServiceDesk.Infrastructure.Identity;

public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public IdentityService(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IdentityUserRecord> RegisterRequesterAsync(string email, string password, CancellationToken cancellationToken)
    {
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            throw new ValidationException("A user with this email already exists.");
        }

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var firstError = result.Errors.FirstOrDefault()?.Description ?? "Failed to register user.";
            throw new ValidationException(firstError);
        }

        if (!await _roleManager.RoleExistsAsync(ApplicationRoles.Requester))
        {
            await _roleManager.CreateAsync(new IdentityRole(ApplicationRoles.Requester));
        }

        var roleResult = await _userManager.AddToRoleAsync(user, ApplicationRoles.Requester);
        if (!roleResult.Succeeded)
        {
            var firstError = roleResult.Errors.FirstOrDefault()?.Description ?? "Failed to assign role.";
            throw new ValidationException(firstError);
        }

        return new IdentityUserRecord(user.Id, user.Email!, [ApplicationRoles.Requester]);
    }

    public async Task<IdentityUserRecord?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return null;

        var isValid = await _userManager.CheckPasswordAsync(user, password);
        if (!isValid)
            return null;

        var roles = await _userManager.GetRolesAsync(user);
        return new IdentityUserRecord(user.Id, user.Email ?? email, roles.ToList());
    }

    public async Task<IdentityUserRecord?> GetUserAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return null;

        var roles = await _userManager.GetRolesAsync(user);
        return new IdentityUserRecord(user.Id, user.Email ?? string.Empty, roles.ToList());
    }

    public async Task<bool> IsInRoleAsync(string userId, string role, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return false;

        return await _userManager.IsInRoleAsync(user, role);
    }

    public async Task SetRoleAsync(string userId, string role, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            throw new NotFoundException($"User '{userId}' not found.");

        if (!await _roleManager.RoleExistsAsync(role))
        {
            await _roleManager.CreateAsync(new IdentityRole(role));
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                var error = removeResult.Errors.FirstOrDefault()?.Description ?? "Failed to remove existing roles.";
                throw new ValidationException(error);
            }
        }

        var addResult = await _userManager.AddToRoleAsync(user, role);
        if (!addResult.Succeeded)
        {
            var error = addResult.Errors.FirstOrDefault()?.Description ?? "Failed to assign role.";
            throw new ValidationException(error);
        }
    }
}
