namespace ServiceDesk.Modules.Knowledge.Application.GetKnowledgeTags;

using ServiceDesk.Modules.Knowledge.Application.Contracts;
using ServiceDesk.Modules.Knowledge.Application.Ports;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;

internal sealed class GetKnowledgeTagsUseCase(IKnowledgeQueries queries, ICurrentUser currentUser) : IGetKnowledgeTags
{
    public Task<IReadOnlyList<TagSummaryDto>> ExecuteAsync(GetKnowledgeTagsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        _ = AuthenticatedActor.RequireId(currentUser);
        return queries.GetTagsAsync(cancellationToken);
    }
}
