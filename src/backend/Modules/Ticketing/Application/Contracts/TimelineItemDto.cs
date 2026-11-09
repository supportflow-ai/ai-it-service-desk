namespace ServiceDesk.Modules.Ticketing.Application.Contracts;

/// <summary>DTO biểu diễn một dòng hoạt động / trao đổi trên timeline.</summary>
public record TimelineItemDto(
    Guid Id,
    Guid TicketId,
    Guid AuthorId,
    string AuthorName,
    string AuthorRole,
    string Content,
    bool IsInternal,
    DateTime CreatedAt
);
