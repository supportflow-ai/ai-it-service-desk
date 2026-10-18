using ServiceDesk.Application.Identity.Interfaces;
using ServiceDesk.Application.Identity.Models;

namespace ServiceDesk.Application.Identity;

public sealed class AuthService(IUserAccountGateway accounts, IAccessTokenIssuer tokens) : IAuthService
{
    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var result = await accounts.CreateRequesterAsync(request.Email, request.Password, cancellationToken);
        if (result.User is null) return AuthResult.Failed(result.Errors);
        var user = result.User with { FullName = request.FullName };
        return AuthResult.Success(tokens.GenerateToken(user), user);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await accounts.CheckPasswordAsync(request.Email, request.Password, cancellationToken);
        return user is null ? AuthResult.Failed("Invalid email or password.") : AuthResult.Success(tokens.GenerateToken(user), user);
    }

    public Task<UserInfo?> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default) =>
        accounts.FindByIdAsync(userId, cancellationToken);
}
