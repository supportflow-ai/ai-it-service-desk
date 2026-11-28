using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.Ticketing.Application.ReconcileAttachments;

internal sealed class ReconcileAttachmentsUseCase(IAttachmentReconciliationStore store, IObjectStorageService storage) : IReconcileAttachments
{
    public async Task<AttachmentReconciliationResult> ExecuteAsync(AttachmentReconciliationCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Prefix is not ("uploads/pending/" or "tickets/" or "downloads/quarantine/")) throw new ArgumentException("Unsupported reconciliation prefix.");
        if (command.Retention <= TimeSpan.FromSeconds(Math.Max(storage.DownloadExpirySeconds, 120) + 30))
            throw new ArgumentException("Retention must exceed download lifetime and active processing time.");
        var now = await store.GetUtcNowAsync(cancellationToken);
        var cutoff = now.Subtract(command.Retention);
        var page = await storage.ListAsync(storage.DefaultBucket, command.Prefix, command.ContinuationToken, cancellationToken);
        var deleted = 0; var failed = 0;
        foreach (var value in page.Objects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value.LastModifiedUtc >= cutoff) continue;
            try
            {
                if (!await store.MayDeleteObjectAsync(value.Key, cancellationToken)) continue;
                await storage.DeleteAsync(storage.DefaultBucket, value.Key, cancellationToken);
                deleted++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception) { failed++; }
        }
        var pruned = command.Prefix == "uploads/pending/" ? await store.PruneExpiredGrantsAsync(cutoff, cancellationToken) : 0;
        return new(page.Objects.Count, deleted, failed, pruned, page.ContinuationToken);
    }
}
