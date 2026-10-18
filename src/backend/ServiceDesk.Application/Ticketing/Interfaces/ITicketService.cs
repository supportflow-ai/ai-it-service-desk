using ServiceDesk.Application.Ticketing.Commands;
using ServiceDesk.Application.Ticketing.Dtos;

namespace ServiceDesk.Application.Ticketing.Interfaces;

public interface ITicketService
{
    Task<TicketDto> CreateTicketAsync(CreateTicketCommand command, string? idempotencyKey = null, CancellationToken cancellationToken = default);
    Task<PagedTicketResult> GetMyTicketsAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<TicketDto?> GetTicketByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
