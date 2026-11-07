using ServiceDesk.Modules.Identity.Domain;

namespace ServiceDesk.Modules.Identity.Application.Ports;

/// <summary>
/// Cổng lưu refresh token (chỉ lưu giá trị băm). Mọi thao tác ghi giữ khóa phiên của người dùng trong giao dịch của mình,
/// nên thu hồi và xoay vòng chạy đồng thời không để token nào lọt qua.
/// </summary>
public interface IRefreshTokenStore
{
    /// <summary>Tìm token theo giá trị băm bất kể đã thu hồi hay hết hạn (để phát hiện token bị dùng lại).</summary>
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xoay vòng trong một giao dịch: thu hồi có điều kiện token hiện tại rồi lưu <paramref name="replacement"/>.
    /// Trả về false (không lưu gì) khi token hiện tại đã bị thu hồi.
    /// </summary>
    Task<bool> TryRotateAsync(Guid currentTokenId, RefreshToken replacement, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Thu hồi mọi token còn hiệu lực trong chuỗi phiên <paramref name="familyId"/> của người dùng.</summary>
    Task RevokeFamilyAsync(string userId, Guid familyId, DateTime revokedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đăng xuất bằng một refresh token: thu hồi cả chuỗi phiên của nó, kể cả khi token đã được xoay vòng
    /// (client giữ token cũ) hoặc token thay thế vừa được tạo bởi một lần làm mới đang chạy. Token không tồn tại thì bỏ qua.
    /// </summary>
    Task RevokeSessionByTokenAsync(string tokenHash, DateTime revokedAt, CancellationToken cancellationToken = default);
}
