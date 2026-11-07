namespace ServiceDesk.Modules.Knowledge.Application.SearchKnowledge;

using ServiceDesk.Modules.Knowledge.Application.Contracts;

public interface ISearchKnowledge
{
    Task<PagedArticleResult> ExecuteAsync(SearchKnowledgeQuery query, CancellationToken cancellationToken = default);
}
