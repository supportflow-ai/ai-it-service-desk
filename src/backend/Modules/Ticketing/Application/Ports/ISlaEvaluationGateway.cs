using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.Ports;

/// <summary>
/// Cổng truy vấn và cập nhật trạng thái vi phạm SLA cho Background Worker.
/// </summary>
public interface ISlaEvaluationGateway
{
    /// <summary>
    /// Lấy một batch ticket cần đánh giá SLA theo thứ tự ID ổn định (Keyset pagination).
    /// </summary>
    Task<IReadOnlyList<Ticket>> GetActiveSlaTicketsBatchAsync(Guid? afterId, int batchSize, CancellationToken cancellationToken);

    /// <summary>
    /// Lưu trạng thái vi phạm của ticket. Trả về true nếu thành công, false nếu bị xung đột đồng thời (concurrency conflict).
    /// </summary>
    Task<bool> UpdateBreachedMarkersAsync(Ticket ticket, CancellationToken cancellationToken);
}
