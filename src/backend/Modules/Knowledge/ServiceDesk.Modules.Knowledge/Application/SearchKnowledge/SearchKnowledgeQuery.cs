namespace ServiceDesk.Modules.Knowledge.Application.SearchKnowledge;

using ServiceDesk.Modules.Knowledge.Domain;

public record SearchKnowledgeQuery(
    string? Query = null,
    string? Tag = null,
    ArticleStatus? Status = null,
    int Page = 1,
    int PageSize = 10
);
