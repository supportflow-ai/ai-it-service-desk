namespace ServiceDesk.Application.Knowledge.Ports;

using ServiceDesk.Application.Knowledge.Authorization;
using ServiceDesk.Application.Knowledge.Contracts;
using ServiceDesk.Domain.Knowledge;

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
