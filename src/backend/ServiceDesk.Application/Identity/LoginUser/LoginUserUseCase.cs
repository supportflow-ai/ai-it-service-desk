using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Identity.Ports;

namespace ServiceDesk.Application.Identity.LoginUser;

/// <summary>Hợp đồng use case đăng nhập hệ thống.</summary>
public interface ILoginUser
{
    Task<AuthResult> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý nghiệp vụ đăng nhập:
/// - Xác thực email và mật khẩu (có cơ chế row-lock bảo vệ lockout nhất quán).
/// - Trả về token JWT và thông tin user nếu xác thực thành công.
/// </summary>
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
