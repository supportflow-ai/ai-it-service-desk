namespace ServiceDesk.Modules.Knowledge.Application.CreateDraftArticle;

using ServiceDesk.Modules.Knowledge.Application.Contracts;

public interface ICreateDraftArticle
{
    Task<ArticleDto> ExecuteAsync(CreateDraftArticleCommand command, CancellationToken cancellationToken = default);
}
