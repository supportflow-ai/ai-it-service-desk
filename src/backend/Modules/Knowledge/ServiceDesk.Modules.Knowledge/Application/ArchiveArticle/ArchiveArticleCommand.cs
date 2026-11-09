namespace ServiceDesk.Modules.Knowledge.Application.ArchiveArticle;

public record ArchiveArticleCommand(Guid Id, uint? ExpectedRevision = null);
