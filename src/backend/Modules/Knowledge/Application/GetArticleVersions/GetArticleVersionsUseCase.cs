namespace ServiceDesk.Modules.Knowledge.Application.GetArticleVersions;

using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Knowledge.Application.Authorization;
using ServiceDesk.Modules.Knowledge.Application.Contracts;
using ServiceDesk.Modules.Knowledge.Application.Ports;

internal sealed class GetArticleVersionsUseCase(
    IKnowledgeQueries queries,
    ICurrentUser currentUser) : IGetArticleVersions
{
    public Task<IReadOnlyList<ArticleVersionDto>> ExecuteAsync(GetArticleVersionsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return queries.GetVersionsAsync(query.ArticleId, KnowledgePermissions.ReadScope(currentUser), cancellationToken);
    }
}
