namespace ServiceDesk.Application.Ticketing.Dtos;

public record TicketDto(
    Guid Id,
    string TicketNumber,
    string Title,
    string Description,
    string CategoryId,
    int Status,
    DateTime CreatedAt
);
