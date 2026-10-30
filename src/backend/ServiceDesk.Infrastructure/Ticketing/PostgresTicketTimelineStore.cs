using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Ticketing;
using ServiceDesk.Infrastructure.Persistence;

namespace ServiceDesk.Infrastructure.Ticketing;

internal sealed class PostgresTicketTimelineStore(ApplicationDbContext context) : ITicketTimelineStore
{
    public async Task<TimelineItemDto> AddAsync(TicketComment comment, CancellationToken cancellationToken = default)
    {
        context.TicketComments.Add(comment);
        await context.SaveChangesAsync(cancellationToken);
        return new TimelineItemDto(
            comment.Id,
            comment.TicketId,
            comment.AuthorId,
            comment.AuthorName,
            comment.AuthorRole,
            comment.Content,
            comment.IsInternal,
            comment.CreatedAt);
    }

    public async Task<IReadOnlyList<TimelineItemDto>> GetForTicketAsync(Guid ticketId, bool includeInternal, CancellationToken cancellationToken = default)
    {
        var query = context.TicketComments.AsNoTracking().Where(c => c.TicketId == ticketId);
        if (!includeInternal)
        {
            query = query.Where(c => !c.IsInternal);
        }

        return await query
            .OrderBy(c => c.CreatedAt)
            .Select(c => new TimelineItemDto(
                c.Id,
                c.TicketId,
                c.AuthorId,
                c.AuthorName,
                c.AuthorRole,
                c.Content,
                c.IsInternal,
                c.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
