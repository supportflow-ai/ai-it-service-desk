namespace ServiceDesk.Modules.Knowledge.Application.RepublishArticle;

using ServiceDesk.Modules.Knowledge.Application.Contracts;

public interface IRepublishArticle
{
    Task<ArticleDto> ExecuteAsync(RepublishArticleCommand command, CancellationToken cancellationToken = default);
}
