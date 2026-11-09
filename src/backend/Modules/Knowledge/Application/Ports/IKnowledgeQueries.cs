namespace ServiceDesk.Modules.Knowledge.Application.Ports;

using ServiceDesk.Modules.Knowledge.Application.Authorization;
using ServiceDesk.Modules.Knowledge.Application.Contracts;
using ServiceDesk.Modules.Knowledge.Domain;

public interface IKnowledgeQueries
{
    Task<ArticleDto?> GetByIdAsync(Guid id, KnowledgeReadScope scope, CancellationToken cancellationToken = default);
    Task<PagedArticleResult> SearchPublishedAsync(string? query, string? tag, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedArticleResult> SearchStaffAsync(
        string? query,
        string? tag,
        ArticleStatus? status,
        KnowledgeReadScope scope,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
