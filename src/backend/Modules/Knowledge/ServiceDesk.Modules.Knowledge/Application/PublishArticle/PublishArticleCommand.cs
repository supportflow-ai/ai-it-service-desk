namespace ServiceDesk.Modules.Knowledge.Application.PublishArticle;

public record PublishArticleCommand(Guid Id, uint? ExpectedRevision = null);
