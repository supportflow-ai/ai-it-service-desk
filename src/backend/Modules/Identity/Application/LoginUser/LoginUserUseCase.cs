using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Identity.Application.Common;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Ports;

namespace ServiceDesk.Modules.Identity.Application.LoginUser;

/// <summary>Hợp đồng use case đăng nhập hệ thống.</summary>
public interface ILoginUser
{
    Task<AuthResult> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý nghiệp vụ đăng nhập:
/// - Xác thực email và mật khẩu (có cơ chế row-lock bảo vệ lockout nhất quán).
/// - Trả về token JWT, refresh token và thông tin user nếu xác thực thành công.
/// </summary>
internal sealed class LoginUserUseCase(
    IUserAccountGateway accounts,
    IAccessTokenIssuer tokens,
    IClock clock) : ILoginUser
{
    public async Task<AuthResult> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Kiểm mật khẩu và lưu refresh token trong cùng một giao dịch (xem IUserAccountGateway.SignInAsync).
        var signIn = await accounts.SignInAsync(request.Email, request.Password, clock.UtcNow.UtcDateTime, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (signIn is null)
        {
            return AuthResult.Failed("Invalid email or password.");
        }

        return AuthResult.Success(tokens.GenerateToken(signIn.User), signIn.User, signIn.RawRefreshToken);
    }
}
