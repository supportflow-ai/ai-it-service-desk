namespace ServiceDesk.Application.Knowledge.UpdateDraftArticle;

using ServiceDesk.Application.Knowledge.Contracts;

public interface IUpdateDraftArticle
{
    Task<ArticleDto> ExecuteAsync(UpdateDraftArticleCommand command, CancellationToken cancellationToken = default);
}
