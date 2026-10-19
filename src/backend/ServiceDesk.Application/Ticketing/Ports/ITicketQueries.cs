using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Authorization;

namespace ServiceDesk.Application.Ticketing.Ports;

public interface ITicketQueries
{
    Task<PagedTicketResult> GetForRequesterAsync(Guid requesterId, int page, int pageSize, CancellationToken cancellationToken);
    Task<TicketDto?> GetByIdAsync(Guid id, TicketReadScope scope, CancellationToken cancellationToken);
}
