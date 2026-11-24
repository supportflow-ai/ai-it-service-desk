namespace ServiceDesk.Modules.Knowledge.Application.GetArticleVersions;

using ServiceDesk.Modules.Knowledge.Application.Contracts;

public interface IGetArticleVersions
{
    Task<IReadOnlyList<ArticleVersionDto>> ExecuteAsync(GetArticleVersionsQuery query, CancellationToken cancellationToken = default);
}
