using System.Security.Cryptography;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Identity.Common;
using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Identity.Ports;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Identity.LoginUser;

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

        var rawRefreshToken = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(64));
        var now = clock.UtcNow.UtcDateTime;
        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            TokenHash = TokenHasher.Hash(rawRefreshToken),
            CreatedAt = now,
            ExpiresAt = now.AddDays(7)
        };
        await refreshTokens.SaveTokenAsync(refreshTokenEntity, cancellationToken);

        return AuthResult.Success(tokens.GenerateToken(user), user, rawRefreshToken);
    }
}
