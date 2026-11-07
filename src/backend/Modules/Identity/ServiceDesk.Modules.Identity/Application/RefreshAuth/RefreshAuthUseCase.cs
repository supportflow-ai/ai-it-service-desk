using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Identity.Application.Common;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Ports;

namespace ServiceDesk.Modules.Identity.Application.RefreshAuth;

public interface IRefreshAuth
{
    Task<AuthResult> ExecuteAsync(RefreshAuthRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xoay vòng refresh token:
/// - Token đã thu hồi mà vẫn bị gửi lại được coi là bị lộ: thu hồi mọi token của người dùng.
/// - Thu hồi token cũ bằng lệnh cập nhật có điều kiện; request đến sau với cùng token bị từ chối.
/// - Cấp access token theo vai trò hiện tại trong cơ sở dữ liệu.
/// </summary>
internal sealed class RefreshAuthUseCase(
    IRefreshTokenStore refreshTokens,
    IUserAccountGateway accounts,
    IAccessTokenIssuer tokens,
    IClock clock) : IRefreshAuth
{
    private const string InvalidToken = "Invalid or expired refresh token.";

    public async Task<AuthResult> ExecuteAsync(RefreshAuthRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return AuthResult.Failed("Refresh token is required.");
        }

        var now = clock.UtcNow.UtcDateTime;
        var tokenHash = TokenHasher.Hash(request.RefreshToken.Trim());
        var stored = await refreshTokens.FindByHashAsync(tokenHash, cancellationToken);
        if (stored is null)
        {
            return AuthResult.Failed(InvalidToken);
        }

        if (stored.RevokedAt is not null)
        {
            // Token đã bị thay thế hoặc thu hồi nhưng vẫn được dùng lại: coi như bị lộ.
            await refreshTokens.RevokeAllForUserAsync(stored.UserId, now, cancellationToken);
            return AuthResult.Failed(InvalidToken);
        }

        if (stored.ExpiresAt <= now)
        {
            return AuthResult.Failed(InvalidToken);
        }

        var user = await accounts.FindByIdAsync(stored.UserId, cancellationToken);
        if (user is null)
        {
            return AuthResult.Failed("User account no longer exists.");
        }

        if (!await refreshTokens.TryRevokeAsync(stored.Id, now, cancellationToken))
        {
            // Một request khác vừa dùng cùng token: không cấp thêm phiên.
            await refreshTokens.RevokeAllForUserAsync(stored.UserId, now, cancellationToken);
            return AuthResult.Failed(InvalidToken);
        }

        var (next, rawRefreshToken) = RefreshTokenIssuer.Issue(user.UserId, now, stored.DeviceInfo, stored.IpAddress);
        await refreshTokens.SaveTokenAsync(next, cancellationToken);

        return AuthResult.Success(tokens.GenerateToken(user), user, rawRefreshToken);
    }
}
