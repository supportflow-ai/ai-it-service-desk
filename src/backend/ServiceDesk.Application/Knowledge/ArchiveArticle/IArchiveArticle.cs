namespace ServiceDesk.Application.Knowledge.ArchiveArticle;

using ServiceDesk.Application.Knowledge.Contracts;

public interface IArchiveArticle
{
    Task<ArticleDto> ExecuteAsync(ArchiveArticleCommand command, CancellationToken cancellationToken = default);
}
