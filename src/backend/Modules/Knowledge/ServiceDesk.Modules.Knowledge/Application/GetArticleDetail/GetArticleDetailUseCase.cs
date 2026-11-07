namespace ServiceDesk.Modules.Knowledge.Application.GetArticleDetail;

using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Knowledge.Application.Authorization;
using ServiceDesk.Modules.Knowledge.Application.Contracts;
using ServiceDesk.Modules.Knowledge.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;

internal sealed class GetArticleDetailUseCase(
    IKnowledgeQueries queries,
    ICurrentUser currentUser) : IGetArticleDetail
{
    public Task<ArticleDto?> ExecuteAsync(GetArticleDetailQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        KnowledgeReadScope scope;
        if (currentUser.IsInRole(RoleNames.Admin))
        {
            scope = new KnowledgeReadScope.Staff();
        }
        else if (currentUser.IsInRole(RoleNames.Agent))
        {
            scope = new KnowledgeReadScope.Author(AuthenticatedActor.RequireId(currentUser));
        }
        else
        {
            scope = new KnowledgeReadScope.Public();
        }

        return queries.GetByIdAsync(query.Id, scope, cancellationToken);
    }
}
