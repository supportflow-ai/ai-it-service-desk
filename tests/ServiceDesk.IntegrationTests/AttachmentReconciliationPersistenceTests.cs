using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ServiceDesk.Platform.Persistence;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.Modules.Ticketing.Infrastructure;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class AttachmentReconciliationPersistenceTests(TicketIdempotencyPostgresFixture fixture) : IClassFixture<TicketIdempotencyPostgresFixture>
{
    [Fact]
    public async Task ActiveIntentAndReferencesAreProtectedAndExpiredOwnerCannotCommitAfterCleanupApproval()
    {
        var grant = await GrantAsync();
        string oldKey;
        await using (var old = fixture.CreateContext())
        {
            var reserved = await new PostgresTicketAttachmentStore(old).ReserveConfirmationAsync(grant.Id, Guid.NewGuid());
            oldKey = reserved.ConfirmationStorageKey();
        }
        await using (var active = fixture.CreateContext())
        {
            var check = new PostgresAttachmentReconciliationStore(active);
            Assert.False(await check.MayDeleteObjectAsync(grant.StorageKey));
            Assert.False(await check.MayDeleteObjectAsync(oldKey));
            await active.Database.ExecuteSqlInterpolatedAsync($"UPDATE public.attachment_upload_grants SET confirmation_lease_until = clock_timestamp() - interval '1 second' WHERE \"Id\" = {grant.Id}");
            Assert.True(await check.MayDeleteObjectAsync(oldKey));
        }
        await using (var stale = fixture.CreateContext())
        {
            await Assert.ThrowsAsync<ArgumentException>(() => new PostgresTicketAttachmentStore(stale).ConfirmAsync(grant.Id, Attachment(grant, oldKey), DateTime.UtcNow));
        }
        string final;
        await using (var retry = fixture.CreateContext())
        {
            var store = new PostgresTicketAttachmentStore(retry);
            var reserved = await store.ReserveConfirmationAsync(grant.Id, Guid.NewGuid()); final = reserved.ConfirmationStorageKey();
            Assert.NotEqual(oldKey, final);
            await store.ConfirmAsync(grant.Id, Attachment(grant, final), DateTime.UtcNow);
        }
        await using var verify = fixture.CreateContext();
        var cleanup = new PostgresAttachmentReconciliationStore(verify);
        Assert.False(await cleanup.MayDeleteObjectAsync(final));
        Assert.True(await cleanup.MayDeleteObjectAsync(oldKey));
        Assert.True(await cleanup.MayDeleteObjectAsync(grant.StorageKey));
        Assert.Single(await verify.TicketAttachments.Where(value => value.TicketId == grant.TicketId).ToListAsync());
        Assert.False(await cleanup.MayDeleteObjectAsync($"tickets/{grant.TicketId}/legacy.txt"));
    }

[Fact]
    public async Task CleanupWaitingForConfirmationRechecksTheCommittedReference()
    {
        var grant = await GrantAsync(); string key;
        await using (var reserve = fixture.CreateContext()) key = (await new PostgresTicketAttachmentStore(reserve).ReserveConfirmationAsync(grant.Id, Guid.NewGuid())).ConfirmationStorageKey();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var writer = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.ConnectionString).AddInterceptors(new PauseCommit(entered, release)).Options);
        var confirming = new PostgresTicketAttachmentStore(writer).ConfirmAsync(grant.Id, Attachment(grant, key), DateTime.UtcNow);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await using var cleaner = fixture.CreateContext();
        var deciding = new PostgresAttachmentReconciliationStore(cleaner).MayDeleteObjectAsync(key);
        release.TrySetResult();
        await confirming;
        Assert.False(await deciding);
        Assert.True(await cleaner.TicketAttachments.AnyAsync(value => value.StorageKey == key));
    }

    private sealed class PauseCommit(TaskCompletionSource entered, TaskCompletionSource release) : DbTransactionInterceptor
    {
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<TicketAttachment>().Any())
            {
                entered.TrySetResult(); await release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    [Fact]
    public async Task PruningKeepsActiveLeasesAndReferencedGrantsAndIsRepeatable()
    {
        var expired = await GrantAsync(); var active = await GrantAsync(); var confirmed = await GrantAsync();
        await using (var setup = fixture.CreateContext())
        {
            var store = new PostgresTicketAttachmentStore(setup);
            var reserved = await store.ReserveConfirmationAsync(active.Id, Guid.NewGuid());
            var winner = await store.ReserveConfirmationAsync(confirmed.Id, Guid.NewGuid());
            await store.ConfirmAsync(confirmed.Id, Attachment(confirmed, winner.ConfirmationStorageKey()), DateTime.UtcNow);
            await setup.Database.ExecuteSqlInterpolatedAsync($"UPDATE public.attachment_upload_grants SET expires_at = clock_timestamp() - interval '2 days' WHERE \"Id\" IN ({expired.Id}, {active.Id}, {confirmed.Id})");
        }
        await using var cleanup = fixture.CreateContext();
        var gateway = new PostgresAttachmentReconciliationStore(cleanup);
        var cutoff = (await gateway.GetUtcNowAsync()).AddDays(-1);
        Assert.Equal(1, await gateway.PruneExpiredGrantsAsync(cutoff));
        Assert.Equal(0, await gateway.PruneExpiredGrantsAsync(cutoff));
        Assert.False(await cleanup.AttachmentUploadGrants.AnyAsync(g => g.Id == expired.Id));
        Assert.True(await cleanup.AttachmentUploadGrants.AnyAsync(g => g.Id == active.Id));
        Assert.True(await cleanup.AttachmentUploadGrants.AnyAsync(g => g.Id == confirmed.Id));
    }

    private async Task<AttachmentUploadGrant> GrantAsync()
    {
        await using var db = fixture.CreateContext(); var actor = Guid.NewGuid();
        var ticket = Ticket.Create(actor, "Orphan reconciliation", "Disposable persistence", TicketCategories.NET, DateTime.UtcNow);
        var grant = AttachmentUploadGrant.CreatePending(ticket.Id, actor, "file.txt", 5, "text/plain", DateTime.UtcNow.AddMinutes(15));
        db.Tickets.Add(ticket); db.AttachmentUploadGrants.Add(grant); await db.SaveChangesAsync(); return grant;
    }

    private static TicketAttachment Attachment(AttachmentUploadGrant grant, string key) =>
        TicketAttachment.Create(grant.TicketId, grant.FileName, grant.FileSizeBytes, grant.ContentType, key, grant.ActorId, DateTime.UtcNow);
}
