namespace ServiceDesk.Modules.Knowledge.Application.PublishArticle;

using ServiceDesk.Modules.Knowledge.Application.Contracts;

public interface IPublishArticle
{
    Task<ArticleDto> ExecuteAsync(PublishArticleCommand command, CancellationToken cancellationToken = default);
}
