using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.Ticketing;

public sealed record CreateTicketRequest(
    string Title,
    string Description,
    string Category,
    Impact Impact,
    Urgency Urgency);

public sealed record UpdateTicketRequest(
    string Title,
    string Description,
    string Category,
    Impact Impact,
    Urgency Urgency);

public sealed record AssignTicketRequest(string AssigneeId);

public sealed record AddCommentRequest(string Content);

public sealed record TicketListRequest(
    TicketStatus? Status,
    string? AssigneeId,
    string? RequesterId,
    int Page = 1,
    int PageSize = 20);

public sealed record TicketResponse(
    Guid Id,
    string RequesterId,
    string? AssigneeId,
    string Title,
    string Description,
    string Category,
    Impact Impact,
    Urgency Urgency,
    Priority Priority,
    TicketStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? CancelledAt);

public sealed record TicketCommentResponse(
    Guid Id,
    Guid TicketId,
    string AuthorId,
    string Content,
    DateTimeOffset CreatedAt);

public sealed record PageResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
