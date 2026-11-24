using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.Contracts;

/// <summary>
/// DTO chứa thông tin snapshot cần thiết cho AI phân loại vé mà không làm rò rỉ thực thể Ticket.
/// </summary>
public sealed record TicketTriageSnapshotDto(
    Guid TicketId,
    long Revision,
    string Title,
    string Description,
    string CategoryId,
    Impact? Impact,
    Urgency? Urgency,
    Priority? Priority,
    TicketStatus Status,
    Guid RequesterId
);

/// <summary>
/// Cổng giao tiếp cho phép module AIAssistance truy xuất snapshot và áp dụng quyết định triage.
/// </summary>
public interface ITicketTriageBridge
{
    Task<TicketTriageSnapshotDto?> GetSnapshotAsync(Guid ticketId, CancellationToken cancellationToken = default);

    Task<bool> ApplyTriageDecisionAsync(
        Guid ticketId,
        Impact impact,
        Urgency urgency,
        string? categoryId,
        string auditReason,
        Guid reviewerId,
        uint? expectedRevision = null,
        CancellationToken cancellationToken = default);
}
