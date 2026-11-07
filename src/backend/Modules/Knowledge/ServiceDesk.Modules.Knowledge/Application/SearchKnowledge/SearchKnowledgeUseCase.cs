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

        if (currentUser.IsInRole(RoleNames.Admin))
        {
            return queries.SearchStaffAsync(query.Query, query.Tag, query.Status, new KnowledgeReadScope.Staff(), page, pageSize, cancellationToken);
        }

        if (currentUser.IsInRole(RoleNames.Agent))
        {
            var authorId = AuthenticatedActor.RequireId(currentUser);
            return queries.SearchStaffAsync(query.Query, query.Tag, query.Status, new KnowledgeReadScope.Author(authorId), page, pageSize, cancellationToken);
        }

        return queries.SearchPublishedAsync(query.Query, query.Tag, page, pageSize, cancellationToken);
    }
}
