namespace ServiceDesk.Application.Ticketing.Contracts;

/// <summary>
/// DTO chứa thông tin chi tiết của một ticket trả về cho client.
/// </summary>
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
