namespace ServiceDesk.Application.Knowledge.CreateDraftArticle;

using ServiceDesk.Application.Knowledge.Contracts;

public interface ICreateDraftArticle
{
    Task<ArticleDto> ExecuteAsync(CreateDraftArticleCommand command, CancellationToken cancellationToken = default);
}
