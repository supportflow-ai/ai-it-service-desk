using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.Common.Interfaces;

public interface ITicketStore
{
    Task AddAsync(Ticket ticket, CancellationToken cancellationToken);
    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<TicketComment>> GetCommentsAsync(Guid ticketId, CancellationToken cancellationToken);
    Task<PagedTickets> ListAsync(TicketListFilter filter, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record TicketListFilter(
    string? RequesterId,
    string? AssigneeId,
    TicketStatus? Status,
    int Page,
    int PageSize);

public sealed record PagedTickets(IReadOnlyList<Ticket> Items, int TotalCount);
