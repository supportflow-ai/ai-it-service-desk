namespace ServiceDesk.Modules.Ticketing.Application.Ports;

public interface IAttachmentReconciliationStore
{
    Task<DateTime> GetUtcNowAsync(CancellationToken cancellationToken = default);
    Task<bool> MayDeleteObjectAsync(string storageKey, CancellationToken cancellationToken = default);
    Task<int> PruneExpiredGrantsAsync(DateTime cutoff, CancellationToken cancellationToken = default);
}
