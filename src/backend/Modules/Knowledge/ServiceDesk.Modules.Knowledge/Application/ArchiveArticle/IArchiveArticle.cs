namespace ServiceDesk.Modules.Knowledge.Application.ArchiveArticle;

using ServiceDesk.Modules.Knowledge.Application.Contracts;

public interface IArchiveArticle
{
    Task<ArticleDto> ExecuteAsync(ArchiveArticleCommand command, CancellationToken cancellationToken = default);
}
