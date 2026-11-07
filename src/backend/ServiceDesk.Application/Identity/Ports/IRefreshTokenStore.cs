using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Identity.Ports;

/// <summary>Cổng lưu refresh token (chỉ lưu giá trị băm).</summary>
public interface IRefreshTokenStore
{
    Task SaveTokenAsync(RefreshToken token, CancellationToken cancellationToken = default);

    Task<RefreshToken?> FindActiveByHashAsync(string tokenHash, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Tìm token theo giá trị băm bất kể đã thu hồi hay hết hạn (để phát hiện token bị dùng lại).</summary>
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Thu hồi đúng một token nếu nó chưa bị thu hồi, bằng một lệnh cập nhật có điều kiện.
    /// Trả về false khi request khác đã thu hồi trước (hai request dùng cùng token).
    /// </summary>
    Task<bool> TryRevokeAsync(Guid tokenId, DateTime revokedAt, CancellationToken cancellationToken = default);

    Task RevokeTokenAsync(string tokenHash, DateTime revokedAt, CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(string userId, DateTime revokedAt, CancellationToken cancellationToken = default);
}
