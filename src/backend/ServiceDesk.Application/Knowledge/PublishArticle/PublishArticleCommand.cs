namespace ServiceDesk.Application.Knowledge.PublishArticle;

public record PublishArticleCommand(Guid Id, uint? ExpectedRevision = null);
