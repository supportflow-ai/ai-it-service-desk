namespace ServiceDesk.Modules.Knowledge.Application.GetArticleDetail;

using ServiceDesk.Modules.Knowledge.Application.Contracts;

public interface IGetArticleDetail
{
    Task<ArticleDto?> ExecuteAsync(GetArticleDetailQuery query, CancellationToken cancellationToken = default);
}
