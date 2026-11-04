namespace ServiceDesk.Application.Knowledge.Contracts;

using ServiceDesk.Domain.Knowledge;

public record ArticleDraftDto(
    Guid Id,
    string Title,
    string Content,
    ArticleStatus Status,
    Guid AuthorId,
    int Version,
    uint Revision,
    IReadOnlyList<string> Tags,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
