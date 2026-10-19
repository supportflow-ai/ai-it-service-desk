using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Identity.Ports;

namespace ServiceDesk.Application.Identity.LoginUser;

public interface ILoginUser
{
    Task<AuthResult> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken = default);
}

internal sealed class LoginUserUseCase(IUserAccountGateway accounts, IAccessTokenIssuer tokens) : ILoginUser
{
    public async Task<AuthResult> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = await accounts.CheckPasswordAsync(request.Email, request.Password, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return user is null ? AuthResult.Failed("Invalid email or password.") : AuthResult.Success(tokens.GenerateToken(user), user);
    }
}
