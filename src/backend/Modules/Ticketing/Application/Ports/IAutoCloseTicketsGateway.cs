using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.Ports;

/// <summary>
/// Cổng truy vấn và đóng tự động các vé đã giải quyết nhưng không có phản hồi thêm (DEC-05).
/// </summary>
public interface IAutoCloseTicketsGateway
{
    /// <summary>
    /// Lấy danh sách các vé ở trạng thái Resolved có thời điểm giải quyết trước <paramref name="olderThanUtc"/> theo keyset pagination.
    /// </summary>
    Task<IReadOnlyList<Ticket>> GetResolvedTicketsEligibleForAutoCloseAsync(
        DateTime olderThanUtc,
        Guid? afterId,
        int batchSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Đóng vé và ghi nhận nhật ký hệ thống trong cùng một giao dịch. Trả về true nếu thành công, false nếu xung đột đồng thời.
    /// </summary>
    Task<bool> CloseWithAuditAsync(
        Ticket ticket,
        DateTime now,
        string auditMessage,
        CancellationToken cancellationToken);
}
