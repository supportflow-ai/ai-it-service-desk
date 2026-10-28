using System.Collections.Concurrent;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Infrastructure.Ticketing;

/// <summary>
/// Triển khai lưu trữ Timeline và Comment trong bộ nhớ an toàn (Thread-safe Concurrent).
/// Bảo đảm không can thiệp thay đổi cấu trúc bảng cơ sở dữ liệu dùng chung theo ADR-014.
/// </summary>
internal sealed class InMemoryTicketTimelineStore : ITicketTimelineStore
{
    private static readonly ConcurrentBag<TimelineItemDto> Storage = [];

    public Task<TimelineItemDto> AddAsync(TicketComment comment, CancellationToken cancellationToken = default)
    {
        var item = new TimelineItemDto(
            comment.Id,
            comment.TicketId,
            comment.AuthorId,
            comment.AuthorName,
            comment.AuthorRole,
            comment.Content,
            comment.IsInternal,
            comment.CreatedAt);

        Storage.Add(item);
        return Task.FromResult(item);
    }

    public Task<IReadOnlyList<TimelineItemDto>> GetForTicketAsync(Guid ticketId, bool includeInternal, CancellationToken cancellationToken = default)
    {
        IEnumerable<TimelineItemDto> items = Storage.Where(i => i.TicketId == ticketId);
        if (!includeInternal)
        {
            items = items.Where(i => !i.IsInternal);
        }

        var result = (IReadOnlyList<TimelineItemDto>)items.OrderBy(i => i.CreatedAt).ToList();
        return Task.FromResult(result);
    }
}
