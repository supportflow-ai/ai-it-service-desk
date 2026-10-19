using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Infrastructure.Persistence;

public sealed class EfTicketStore(ApplicationDbContext db) : ITicketStore
{
    public Task AddAsync(Ticket ticket, CancellationToken cancellationToken)
        => db.Tickets.AddAsync(ticket, cancellationToken).AsTask();

    public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => db.Tickets.Include(ticket => ticket.Comments)
            .SingleOrDefaultAsync(ticket => ticket.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TicketComment>> GetCommentsAsync(Guid ticketId, CancellationToken cancellationToken)
        => await db.TicketComments.AsNoTracking()
            .Where(comment => comment.TicketId == ticketId)
            .OrderBy(comment => comment.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<PagedTickets> ListAsync(TicketListFilter filter, CancellationToken cancellationToken)
    {
        var query = db.Tickets.AsNoTracking().AsQueryable();

        if (filter.RequesterId is not null)
            query = query.Where(ticket => ticket.RequesterId == filter.RequesterId);
        if (filter.AssigneeId is not null)
            query = query.Where(ticket => ticket.AssigneeId == filter.AssigneeId);
        if (filter.Status.HasValue)
            query = query.Where(ticket => ticket.Status == filter.Status.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(ticket => ticket.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedTickets(items, totalCount);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => db.SaveChangesAsync(cancellationToken);
}
