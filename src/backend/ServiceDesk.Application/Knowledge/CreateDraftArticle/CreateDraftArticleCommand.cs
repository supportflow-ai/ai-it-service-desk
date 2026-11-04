namespace ServiceDesk.Application.Knowledge.CreateDraftArticle;

public record CreateDraftArticleCommand(
    string Title,
    string Content,
    IReadOnlyList<string>? Tags = null
);
