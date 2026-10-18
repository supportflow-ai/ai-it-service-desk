using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Ticketing.Dtos;
using ServiceDesk.Application.Ticketing.Interfaces;
using ServiceDesk.Infrastructure.Persistence;

namespace ServiceDesk.Infrastructure.Ticketing;

public sealed class PostgresTicketQueries(ApplicationDbContext context) : ITicketQueries
{
    private readonly ApplicationDbContext _context = context;
    public async Task<PagedTicketResult> GetForRequesterAsync(
        Guid requesterId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.RequesterId == requesterId);

        var totalCount = await query.CountAsync(cancellationToken);
        var tickets = await query
            .OrderByDescending(ticket => ticket.CreatedAt)
            .ThenByDescending(ticket => ticket.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ticket => new TicketDto(
                ticket.Id,
                ticket.TicketNumber,
                ticket.Title,
                ticket.Description,
                ticket.CategoryId,
                (int)ticket.Status,
                ticket.CreatedAt,
                ticket.RequesterDepartmentId))
            .ToListAsync(cancellationToken);

        return new PagedTicketResult(tickets, page, pageSize, totalCount);
    }

    public async Task<TicketDto?> GetByIdAsync(Guid id, Guid? ownerId, CancellationToken cancellationToken)
    {
        var query = _context.Tickets.AsNoTracking().Where(ticket => ticket.Id == id);
        if (ownerId.HasValue) query = query.Where(ticket => ticket.RequesterId == ownerId.Value);
        var ticket = await query.FirstOrDefaultAsync(cancellationToken);
        return ticket is null ? null : TicketProjection.ToDto(ticket);
    }
}
