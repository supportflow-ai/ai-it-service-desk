using ServiceDesk.Modules.Identity.Domain;

namespace ServiceDesk.Modules.Identity.Application.Ports;

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

    /// <summary>
    /// Xoay vòng trong một giao dịch: thu hồi có điều kiện token cũ rồi lưu <paramref name="replacement"/>.
    /// Trả về false (không lưu gì) khi token cũ đã bị thu hồi. Vì token mới chỉ thấy được cùng lúc với việc thu hồi
    /// token cũ, request thua cuộc đua luôn thu hồi toàn bộ phiên sau khi token mới đã tồn tại.
    /// </summary>
    Task<bool> TryRotateAsync(Guid currentTokenId, RefreshToken replacement, DateTime now, CancellationToken cancellationToken = default);

    Task RevokeTokenAsync(string tokenHash, DateTime revokedAt, CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(string userId, DateTime revokedAt, CancellationToken cancellationToken = default);
}
