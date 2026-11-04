namespace ServiceDesk.Application.Knowledge.PublishArticle;

using ServiceDesk.Application.Knowledge.Contracts;

public interface IPublishArticle
{
    Task<ArticleDto> ExecuteAsync(PublishArticleCommand command, CancellationToken cancellationToken = default);
}
