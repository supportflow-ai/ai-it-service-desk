namespace ServiceDesk.Application.Knowledge.UpdateDraftArticle;

public record UpdateDraftArticleCommand(
    Guid Id,
    string Title,
    string Content,
    IReadOnlyList<string>? Tags = null,
    uint? ExpectedRevision = null
);
