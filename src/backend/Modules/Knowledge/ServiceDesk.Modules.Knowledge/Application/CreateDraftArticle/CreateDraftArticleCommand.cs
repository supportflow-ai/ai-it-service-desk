namespace ServiceDesk.Modules.Knowledge.Application.CreateDraftArticle;

public record CreateDraftArticleCommand(
    string Title,
    string Content,
    IReadOnlyList<string>? Tags = null
);
