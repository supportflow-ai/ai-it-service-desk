namespace ServiceDesk.Modules.Knowledge.Application.GetKnowledgeTags;

using ServiceDesk.Modules.Knowledge.Application.Contracts;
using ServiceDesk.Modules.Knowledge.Application.Ports;

internal sealed class GetKnowledgeTagsUseCase(IKnowledgeQueries queries) : IGetKnowledgeTags
{
    public Task<IReadOnlyList<TagSummaryDto>> ExecuteAsync(GetKnowledgeTagsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return queries.GetTagsAsync(cancellationToken);
    }
}
