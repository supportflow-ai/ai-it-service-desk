using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Commands;
using ServiceDesk.Application.Ticketing.Dtos;
using ServiceDesk.Application.Ticketing.Interfaces;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Ticketing;
using ServiceDesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ServiceDesk.Application.Ticketing;

public class TicketService : ITicketService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public TicketService(ApplicationDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<TicketDto> CreateTicketAsync(CreateTicketCommand command, Guid requesterId, CancellationToken cancellationToken = default)
    {
        var ticket = Ticket.Create(
            requesterId,
            command.Title,
            command.Description,
            command.CategoryId,
            DateTime.UtcNow);

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync(cancellationToken);

        // Reload to get DB-generated TicketNumber
        await _context.Entry(ticket).ReloadAsync(cancellationToken);

        return new TicketDto(
            ticket.Id,
            ticket.TicketNumber,
            ticket.Title,
            ticket.Description,
            ticket.CategoryId,
            (int)ticket.Status,
            ticket.CreatedAt,
            ticket.RequesterDepartmentId
        );
    }

    public async Task<PagedTicketResult> GetTicketsForRequesterAsync(
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

    public async Task<TicketDto?> GetTicketByIdAsync(Guid id, Guid requesterId, CancellationToken cancellationToken = default)
    {
        var query = _context.Tickets.AsNoTracking().Where(t => t.Id == id);

        if (_currentUser?.IsInRole(RoleNames.Admin) != true && _currentUser?.IsInRole(RoleNames.Agent) != true)
        {
            query = query.Where(t => t.RequesterId == requesterId);
        }

        var ticket = await query.FirstOrDefaultAsync(cancellationToken);
        if (ticket == null)
            return null;

        return new TicketDto(
            ticket.Id,
            ticket.TicketNumber,
            ticket.Title,
            ticket.Description,
            ticket.CategoryId,
            (int)ticket.Status,
            ticket.CreatedAt,
            ticket.RequesterDepartmentId
        );
    }
}
