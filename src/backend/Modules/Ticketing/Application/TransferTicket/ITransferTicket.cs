using ServiceDesk.Modules.Ticketing.Application.Contracts;

namespace ServiceDesk.Modules.Ticketing.Application.TransferTicket;

public interface ITransferTicket
{
    Task<TicketDto> ExecuteAsync(TransferTicketCommand command, CancellationToken cancellationToken = default);
}
