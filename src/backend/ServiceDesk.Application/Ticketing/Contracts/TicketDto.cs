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
    Guid? RequesterDepartmentId = null,
    Guid RequesterId = default,
    Guid? AssignedToId = null,
    DateTime? AssignedAt = null,
    DateTime? UpdatedAt = null,
    uint Revision = 0,
    string? Priority = null,
    string? Impact = null,
    string? Urgency = null
);
