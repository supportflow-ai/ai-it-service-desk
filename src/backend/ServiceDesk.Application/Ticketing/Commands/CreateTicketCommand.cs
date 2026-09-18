namespace ServiceDesk.Application.Ticketing.Commands;

public record CreateTicketCommand(
    string Title,
    string Description,
    string CategoryId
);
