namespace ServiceDesk.Application.Knowledge.SearchKnowledge;

using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Knowledge.Contracts;
using ServiceDesk.Application.Knowledge.Ports;
using ServiceDesk.Domain.Identity;

internal sealed class SearchKnowledgeUseCase(
    IKnowledgeQueries queries,
    ICurrentUser currentUser) : ISearchKnowledge
{
    public Task<PagedArticleResult> ExecuteAsync(SearchKnowledgeQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        if (currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin))
        {
            return queries.SearchStaffAsync(query.Query, query.Tag, query.Status, page, pageSize, cancellationToken);
        }

        return queries.SearchPublishedAsync(query.Query, query.Tag, page, pageSize, cancellationToken);
    }
}
