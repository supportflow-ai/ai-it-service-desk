namespace ServiceDesk.Application.Knowledge.GetArticleDetail;

using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Knowledge.Authorization;
using ServiceDesk.Application.Knowledge.Contracts;
using ServiceDesk.Application.Knowledge.Ports;
using ServiceDesk.Domain.Identity;

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
            scope = new KnowledgeReadScope.Author(Guid.Parse(currentUser.UserId!));
        }
        else
        {
            scope = new KnowledgeReadScope.Public();
        }

        return queries.GetByIdAsync(query.Id, scope, cancellationToken);
    }
}
