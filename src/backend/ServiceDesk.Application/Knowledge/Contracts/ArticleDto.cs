namespace ServiceDesk.Application.Knowledge.Contracts;

using ServiceDesk.Domain.Knowledge;

public record ArticleDto(
    Guid Id,
    string Title,
    string Content,
    ArticleStatus Status,
    Guid AuthorId,
    int Version,
    uint Revision,
    IReadOnlyList<string> Tags,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? PublishedAt,
    DateTime? ArchivedAt
);

public record PagedArticleResult(
    IReadOnlyList<ArticleDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages
);
