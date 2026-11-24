using ServiceDesk.Modules.Ticketing.Application.Contracts;

namespace ServiceDesk.Modules.Ticketing.Application.TransferTicket;

public record TransferTicketCommand(Guid Id, TransferTicketRequest Request);
