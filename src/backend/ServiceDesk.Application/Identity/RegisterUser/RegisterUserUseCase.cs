using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Identity.Ports;

namespace ServiceDesk.Application.Identity.RegisterUser;

public interface IRegisterUser
{
    Task<AuthResult> ExecuteAsync(RegisterRequest request, CancellationToken cancellationToken = default);
}

internal sealed class RegisterUserUseCase(IUserAccountGateway accounts, IAccessTokenIssuer tokens) : IRegisterUser
{
    public async Task<AuthResult> ExecuteAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await accounts.CreateRequesterAsync(request.Email, request.Password, cancellationToken);
        if (result.User is null) return AuthResult.Failed(result.Errors);
        var user = result.User with { FullName = request.FullName };
        return AuthResult.Success(tokens.GenerateToken(user), user);
    }
}
