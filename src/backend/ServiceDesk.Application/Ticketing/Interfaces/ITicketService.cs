using ServiceDesk.Application.Ticketing.Commands;
using ServiceDesk.Application.Ticketing.Dtos;

namespace ServiceDesk.Application.Ticketing.Interfaces;

public interface ITicketService
{
    Task<TicketDto> CreateTicketAsync(CreateTicketCommand command, Guid requesterId, CancellationToken cancellationToken = default);
    Task<TicketDto?> GetTicketByIdAsync(Guid id, Guid requesterId, CancellationToken cancellationToken = default);
}
