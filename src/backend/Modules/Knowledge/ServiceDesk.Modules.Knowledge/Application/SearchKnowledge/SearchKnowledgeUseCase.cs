namespace ServiceDesk.Modules.Knowledge.Application.SearchKnowledge;

using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Knowledge.Application.Authorization;
using ServiceDesk.Modules.Knowledge.Application.Contracts;
using ServiceDesk.Modules.Knowledge.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;

internal sealed class SearchKnowledgeUseCase(
    IKnowledgeQueries queries,
    ICurrentUser currentUser) : ISearchKnowledge
{
    public Task<PagedArticleResult> ExecuteAsync(SearchKnowledgeQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var scope = KnowledgePermissions.ReadScope(currentUser);
        return scope is KnowledgeReadScope.Public
            ? queries.SearchPublishedAsync(query.Query, query.Tag, page, pageSize, cancellationToken)
            : queries.SearchStaffAsync(query.Query, query.Tag, query.Status, scope, page, pageSize, cancellationToken);
    }
}
