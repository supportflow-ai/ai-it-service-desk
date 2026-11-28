using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Infrastructure;

internal sealed class PostgresAttachmentReconciliationStore(DbContext context) : IAttachmentReconciliationStore
{
    public Task<DateTime> GetUtcNowAsync(CancellationToken cancellationToken = default) =>
        context.Database.SqlQuery<DateTime>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(cancellationToken);

    public async Task<bool> MayDeleteObjectAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var parts = storageKey.Split('/');
        var pending = parts.Length == 4 && parts[0] == "uploads" && parts[1] == "pending" &&
            Guid.TryParseExact(parts[2], "D", out _) && Guid.TryParseExact(parts[3], "D", out _);
        var final = parts.Length == 6 && parts[0] == "tickets" && parts[2] == "attachments" && parts[3] == "v2" &&
            Guid.TryParseExact(parts[1], "D", out _) && Guid.TryParseExact(parts[4], "N", out _) && Guid.TryParseExact(parts[5], "N", out _);
        var download = parts.Length == 3 && parts[0] == "downloads" && parts[1] == "quarantine" && Guid.TryParseExact(parts[2], "N", out _);
        if (!pending && !final && !download) return false;
        if (download) return !await IsReferencedAsync(storageKey, cancellationToken);
        Guid? grantId = final ? Guid.ParseExact(parts[4], "N") : await context.Set<AttachmentUploadGrant>().AsNoTracking()
            .Where(value => value.StorageKey == storageKey).Select(value => (Guid?)value.Id).SingleOrDefaultAsync(cancellationToken);
        if (grantId is null) return !await IsReferencedAsync(storageKey, cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockAsync(grantId.Value, cancellationToken);
        if (await IsReferencedAsync(storageKey, cancellationToken)) return false;
        var upload = await context.Set<AttachmentUploadGrant>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == grantId, cancellationToken);
        if (upload is not null)
        {
            if (upload.TicketId != Guid.Parse(parts[pending ? 2 : 1])) return false;
            var now = await GetUtcNowAsync(cancellationToken);
            if (pending)
            {
                if (upload.ConfirmationLeaseUntil > now || upload.ConsumedAt is null && upload.ExpiresAt > now) return false;
            }
            else if (upload.ConfirmationAttemptId == Guid.ParseExact(parts[5], "N"))
            {
                if (upload.ConfirmationLeaseUntil > now) return false;
                await context.Set<AttachmentUploadGrant>().Where(value => value.Id == grantId && value.AttachmentId == null)
                    .ExecuteUpdateAsync(update => update.SetProperty(value => value.ConfirmationAttemptId, (Guid?)null)
                        .SetProperty(value => value.ConfirmationLeaseUntil, (DateTime?)null), cancellationToken);
            }
        }
        // Sau khi fence, lượt cũ không thể tạo DB reference cho key này khi remote delete chạy.
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<int> PruneExpiredGrantsAsync(DateTime cutoff, CancellationToken cancellationToken = default)
    {
        var candidates = await context.Set<AttachmentUploadGrant>().AsNoTracking()
            .Where(value => value.ExpiresAt < cutoff && value.AttachmentId == null &&
                (value.ConfirmationLeaseUntil == null || value.ConfirmationLeaseUntil < cutoff))
            .OrderBy(value => value.ExpiresAt).Select(value => value.Id).Take(100).ToListAsync(cancellationToken);
        var count = 0;
        foreach (var id in candidates)
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await LockAsync(id, cancellationToken);
            count += await context.Set<AttachmentUploadGrant>().Where(value => value.Id == id && value.ExpiresAt < cutoff && value.AttachmentId == null &&
                (value.ConfirmationLeaseUntil == null || value.ConfirmationLeaseUntil < cutoff)).ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return count;
    }

    private Task<bool> IsReferencedAsync(string storageKey, CancellationToken cancellationToken) =>
        context.Set<TicketAttachment>().AsNoTracking().AnyAsync(value => value.StorageKey == storageKey, cancellationToken);

    private async Task LockAsync(Guid id, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '1s'", cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({id.ToString()}, 0))", cancellationToken);
    }
}
