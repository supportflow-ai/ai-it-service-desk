using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.Ticketing.Ports;

/// <summary>
/// Cổng lưu trữ và truy vấn dòng sự kiện trao đổi (Comment / Internal Note / Timeline).
/// </summary>
public interface ITicketTimelineStore
{
    Task<TimelineItemDto> AddAsync(TicketComment comment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimelineItemDto>> GetForTicketAsync(Guid ticketId, bool includeInternal, CancellationToken cancellationToken = default);
}
