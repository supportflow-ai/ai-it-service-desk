namespace ServiceDesk.Modules.Knowledge.Application.Ports;

using ServiceDesk.Modules.Knowledge.Domain;

public interface IKnowledgeWriter
{
    Task<Article> CreateDraftAsync(Article article, CancellationToken cancellationToken = default);
    Task<Article?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    uint GetRevision(Article article);
    Task<Article> UpdateArticleAsync(Article article, uint? expectedRevision = null, CancellationToken cancellationToken = default);
}
