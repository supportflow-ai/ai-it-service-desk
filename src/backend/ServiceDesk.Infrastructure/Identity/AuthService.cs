using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ServiceDesk.Application.Identity.Interfaces;
using ServiceDesk.Application.Identity.Models;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Infrastructure.Identity;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<IdentityUser> userManager,
        IJwtTokenGenerator jwtTokenGenerator,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _logger = logger;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return AuthResult.Failed("Email is already registered.");
        }

        var user = new IdentityUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return AuthResult.Failed(createResult.Errors.Select(e => e.Description));
        }

        var role = !string.IsNullOrWhiteSpace(request.Role) && RoleNames.All.Contains(request.Role)
            ? request.Role
            : RoleNames.Requester;

        var addRoleResult = await _userManager.AddToRoleAsync(user, role);
        if (!addRoleResult.Succeeded)
        {
            _logger.LogError("Failed to assign role {Role} to user {Email}: {Errors}",
                role, user.Email, string.Join(", ", addRoleResult.Errors.Select(e => e.Description)));
        }

        var roles = new[] { role };
        var token = _jwtTokenGenerator.GenerateToken(user, roles);

        var userInfo = new UserInfo
        {
            UserId = user.Id,
            Email = user.Email!,
            FullName = request.FullName,
            Roles = roles
        };

        return AuthResult.Success(token, userInfo);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return AuthResult.Failed("Invalid email or password.");
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isPasswordValid)
        {
            return AuthResult.Failed("Invalid email or password.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenGenerator.GenerateToken(user, roles);

        var userInfo = new UserInfo
        {
            UserId = user.Id,
            Email = user.Email!,
            FullName = null,
            Roles = roles.ToList()
        };

        return AuthResult.Success(token, userInfo);
    }

    public async Task<UserInfo?> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);
        return new UserInfo
        {
            UserId = user.Id,
            Email = user.Email!,
            Roles = roles.ToList()
        };
    }
}
