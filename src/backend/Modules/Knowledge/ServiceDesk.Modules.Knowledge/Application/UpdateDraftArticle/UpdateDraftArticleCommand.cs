namespace ServiceDesk.Modules.Knowledge.Application.UpdateDraftArticle;

public record UpdateDraftArticleCommand(
    Guid Id,
    string Title,
    string Content,
    IReadOnlyList<string>? Tags = null,
    uint? ExpectedRevision = null
);
