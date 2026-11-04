namespace ServiceDesk.Application.Knowledge.GetArticleDetail;

using ServiceDesk.Application.Knowledge.Contracts;

public interface IGetArticleDetail
{
    Task<ArticleDto?> ExecuteAsync(GetArticleDetailQuery query, CancellationToken cancellationToken = default);
}
