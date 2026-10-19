namespace ServiceDesk.Application.Ticketing.CreateTicket;

public record CreateTicketCommand(
    string Title,
    string Description,
    string CategoryId
);
