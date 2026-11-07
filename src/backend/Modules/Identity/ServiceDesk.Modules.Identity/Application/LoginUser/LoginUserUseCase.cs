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
    IRefreshTokenStore refreshTokens,
    IClock clock) : ILoginUser
{
    public async Task<AuthResult> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = await accounts.CheckPasswordAsync(request.Email, request.Password, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (user is null)
        {
            return AuthResult.Failed("Invalid email or password.");
        }

        var (refreshToken, rawRefreshToken) = RefreshTokenIssuer.Issue(user.UserId, clock.UtcNow.UtcDateTime);
        await refreshTokens.SaveTokenAsync(refreshToken, cancellationToken);

        return AuthResult.Success(tokens.GenerateToken(user), user, rawRefreshToken);
    }
}
