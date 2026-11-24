namespace ServiceDesk.Modules.Knowledge.Application.RepublishArticle;

public record RepublishArticleCommand(
    Guid Id,
    string Title,
    string Content,
    IReadOnlyList<string>? Tags = null,
    uint? ExpectedRevision = null
);
