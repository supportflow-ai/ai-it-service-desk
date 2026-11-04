namespace ServiceDesk.Application.Knowledge.SearchKnowledge;

using ServiceDesk.Domain.Knowledge;

public record SearchKnowledgeQuery(
    string? Query = null,
    string? Tag = null,
    ArticleStatus? Status = null,
    int Page = 1,
    int PageSize = 10
);
