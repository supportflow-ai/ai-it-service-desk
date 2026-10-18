using ServiceDesk.Application.Ticketing.Dtos;

namespace ServiceDesk.Application.Ticketing.Interfaces;

public interface ITicketQueries
{
    Task<PagedTicketResult> GetForRequesterAsync(Guid requesterId, int page, int pageSize, CancellationToken cancellationToken);
    Task<TicketDto?> GetByIdAsync(Guid id, Guid? ownerId, CancellationToken cancellationToken);
}
