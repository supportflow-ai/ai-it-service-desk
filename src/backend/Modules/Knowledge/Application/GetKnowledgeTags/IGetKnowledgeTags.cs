namespace ServiceDesk.Modules.Knowledge.Application.GetKnowledgeTags;

using ServiceDesk.Modules.Knowledge.Application.Contracts;

public interface IGetKnowledgeTags
{
    Task<IReadOnlyList<TagSummaryDto>> ExecuteAsync(GetKnowledgeTagsQuery query, CancellationToken cancellationToken = default);
}
