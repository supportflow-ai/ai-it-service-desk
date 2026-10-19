namespace ServiceDesk.Application.Ticketing.Contracts;

public record TicketDto(
    Guid Id,
    string TicketNumber,
    string Title,
    string Description,
    string CategoryId,
    int Status,
    DateTime CreatedAt,
    Guid? RequesterDepartmentId = null
);
