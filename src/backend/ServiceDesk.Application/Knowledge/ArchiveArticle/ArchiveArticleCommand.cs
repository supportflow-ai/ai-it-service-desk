namespace ServiceDesk.Application.Knowledge.ArchiveArticle;

public record ArchiveArticleCommand(Guid Id, uint? ExpectedRevision = null);
