namespace ServiceDesk.Application.Knowledge.SearchKnowledge;

using ServiceDesk.Application.Knowledge.Contracts;

public interface ISearchKnowledge
{
    Task<PagedArticleResult> ExecuteAsync(SearchKnowledgeQuery query, CancellationToken cancellationToken = default);
}
