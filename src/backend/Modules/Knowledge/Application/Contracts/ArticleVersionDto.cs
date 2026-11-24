namespace ServiceDesk.Modules.Knowledge.Application.Contracts;

public record ArticleVersionDto(
    Guid Id,
    Guid ArticleId,
    int VersionNumber,
    string Title,
    string Content,
    Guid PublishedById,
    DateTime PublishedAt
);
