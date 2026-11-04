namespace ServiceDesk.Application.Knowledge.Ports;

using ServiceDesk.Domain.Knowledge;

public interface IKnowledgeWriter
{
    Task<Article> CreateDraftAsync(Article article, CancellationToken cancellationToken = default);
    Task<Article?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    uint GetRevision(Article article);
    Task<Article> UpdateArticleAsync(Article article, uint? expectedRevision = null, CancellationToken cancellationToken = default);
}
