namespace ServiceDesk.Modules.Knowledge.Application.UpdateDraftArticle;

using ServiceDesk.Modules.Knowledge.Application.Contracts;

public interface IUpdateDraftArticle
{
    Task<ArticleDto> ExecuteAsync(UpdateDraftArticleCommand command, CancellationToken cancellationToken = default);
}
