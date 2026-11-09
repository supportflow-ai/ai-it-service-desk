namespace ServiceDesk.Modules.Knowledge.Application.Contracts;

using ServiceDesk.Modules.Knowledge.Domain;

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
