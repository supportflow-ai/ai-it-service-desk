using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Identity.Ports;

namespace ServiceDesk.Application.Identity.RegisterUser;

/// <summary>Hợp đồng use case đăng ký tài khoản người dùng mới.</summary>
public interface IRegisterUser
{
    Task<AuthResult> ExecuteAsync(RegisterRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý nghiệp vụ đăng ký người dùng:
/// - Tạo tài khoản với vai trò mặc định là Requester.
/// - Phát sinh access token JWT ngay sau khi đăng ký thành công.
/// </summary>
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
