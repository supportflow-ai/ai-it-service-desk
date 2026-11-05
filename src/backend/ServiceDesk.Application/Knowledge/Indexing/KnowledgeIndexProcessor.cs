using ServiceDesk.Application.Common.Interfaces;

namespace ServiceDesk.Application.Knowledge.Indexing;

public sealed record IndexProcessingResult(bool Found, Guid? JobId, string Outcome);

public interface IKnowledgeIndexProcessor
{
    Task<IndexProcessingResult> ProcessNextAsync(TimeSpan leaseDuration, int maxAttempts, CancellationToken ct);
}

internal sealed class KnowledgeIndexProcessor(IKnowledgeIndexStore store, IKnowledgeIndexPreparer preparer,
    IClock clock) : IKnowledgeIndexProcessor
{
    public async Task<IndexProcessingResult> ProcessNextAsync(TimeSpan leaseDuration, int maxAttempts, CancellationToken ct)
    {
        var lease = await store.ClaimAsync(clock.UtcNow.UtcDateTime, leaseDuration, maxAttempts, ct);
        if (lease is null) return new(false, null, "idle");
        try
        {
            var result = await preparer.PrepareAsync(lease.Snapshot, ct);
            result.Validate(lease.Snapshot);
            var activated = await store.CompleteAsync(lease, result, clock.UtcNow.UtcDateTime, ct);
            return new(true, lease.JobId, activated ? "succeeded" : "superseded_or_lease_lost");
        }
        catch (KnowledgeIndexException ex)
        {
            await store.FailAsync(lease, ex.Code, ex.Retryable, clock.UtcNow.UtcDateTime, maxAttempts, ct);
            return new(true, lease.JobId, ex.Code);
        }
    }
}
