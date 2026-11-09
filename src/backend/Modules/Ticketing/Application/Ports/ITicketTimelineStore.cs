using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.Ports;

/// <summary>
/// Cổng lưu trữ và truy vấn dòng sự kiện trao đổi (Comment / Internal Note / Timeline).
/// </summary>
public interface ITicketTimelineStore
{
    /// <summary>Lưu ghi chú nội bộ (được phép cả khi ticket đã kết thúc).</summary>
    Task<TimelineItemDto> AddAsync(TicketComment comment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lưu bình luận công khai chỉ khi ticket chưa đóng hoặc hủy, kiểm trạng thái và ghi trong cùng giao dịch có khóa
    /// dòng ticket: lệnh đóng/hủy commit trước thì bị phát hiện, chạy sau thì phải chờ.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Ticket không tồn tại.</exception>
    /// <exception cref="ArgumentException">Ticket đã đóng hoặc hủy.</exception>
    Task<TimelineItemDto> AddToOpenTicketAsync(TicketComment comment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimelineItemDto>> GetForTicketAsync(Guid ticketId, bool includeInternal, CancellationToken cancellationToken = default);
}
