using Microsoft.EntityFrameworkCore;
using Npgsql;
using ServiceDesk.Modules.Ticketing.Application.ConfirmAttachmentUpload;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.Modules.Ticketing.Infrastructure;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class TicketAttachmentPersistenceTests(TicketIdempotencyPostgresFixture fixture)
    : IClassFixture<TicketIdempotencyPostgresFixture>
{
    private const long MiB = 1024L * 1024;

    [Fact]
    public async Task ConcurrentConfirmations_ReturnOneAttachment()
    {
        var upload = await CreateUploadAsync();
        async Task<Guid> Confirm()
        {
            await using var context = fixture.CreateContext();
            var attachment = TicketAttachment.Create(upload.TicketId, upload.FileName, upload.FileSizeBytes,
                upload.ContentType, $"tickets/{upload.TicketId}/attachments/{Guid.NewGuid()}", upload.ActorId, DateTime.UtcNow);
            return (await new PostgresTicketAttachmentStore(context).ConfirmAsync(upload.Id, attachment, DateTime.UtcNow)).Id;
        }
        var ids = await Task.WhenAll(Confirm(), Confirm());
        Assert.Equal(ids[0], ids[1]);
        await using var verification = fixture.CreateContext();
        Assert.Equal(1, await verification.TicketAttachments.CountAsync(value => value.TicketId == upload.TicketId));
        Assert.Equal(ids[0], (await verification.AttachmentUploadGrants.SingleAsync(value => value.Id == upload.Id)).AttachmentId);
    }

    [Theory]
    [InlineData(9, 1, 1, "10 tệp")]
    [InlineData(4, 10 * MiB, 10 * MiB, "50 MiB")]
    public async Task ConcurrentDistinctGrants_CanOnlyConfirmOneAtQuotaBoundary(
        int existingCount, long existingSize, long nextSize, string quotaMessage)
    {
        var first = await CreateUploadAsync(nextSize);
        var second = AttachmentUploadGrant.CreatePending(first.TicketId, first.ActorId, "other.txt",
            nextSize, "text/plain", DateTime.UtcNow.AddHours(1));
        await using (var setup = fixture.CreateContext())
        {
            setup.TicketAttachments.AddRange(Enumerable.Range(0, existingCount).Select(_ => TicketAttachment.Create(
                first.TicketId, "existing.txt", existingSize, "text/plain", $"tickets/{first.TicketId}/attachments/{Guid.NewGuid()}",
                first.ActorId, DateTime.UtcNow)));
            await setup.SaveChangesAsync();
            await new PostgresTicketAttachmentStore(setup).CreateUploadGrantAsync(second);
        }

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<Exception?> TryConfirm(AttachmentUploadGrant upload)
        {
            await using var context = fixture.CreateContext();
            var attachment = CreateAttachment(upload);
            await start.Task;
            return await Record.ExceptionAsync(() =>
                new PostgresTicketAttachmentStore(context).ConfirmAsync(upload.Id, attachment, DateTime.UtcNow));
        }

        var firstAttempt = TryConfirm(first);
        var secondAttempt = TryConfirm(second);
        start.SetResult();
        var results = await Task.WhenAll(firstAttempt, secondAttempt);
        Assert.Single(results, result => result is null);
        Assert.Contains(quotaMessage, Assert.IsType<ArgumentException>(Assert.Single(results.OfType<Exception>())).Message);

        await using var verification = fixture.CreateContext();
        var confirmed = verification.TicketAttachments.Where(file => file.TicketId == first.TicketId);
        Assert.Equal(existingCount + 1, await confirmed.CountAsync());
        Assert.Equal(existingCount * existingSize + nextSize, await confirmed.SumAsync(file => file.FileSizeBytes));

        var grants = await verification.AttachmentUploadGrants.AsNoTracking()
            .Where(upload => upload.Id == first.Id || upload.Id == second.Id).ToListAsync();
        var winner = Assert.Single(grants, upload => upload.AttachmentId.HasValue);
        Assert.Null(Assert.Single(grants, upload => upload.Id != winner.Id).ConsumedAt);
        var replay = await new PostgresTicketAttachmentStore(verification).ConfirmAsync(
            winner.Id, CreateAttachment(winner), DateTime.UtcNow);
        Assert.Equal(winner.AttachmentId, replay.Id);
        Assert.Equal(existingCount + 1, await confirmed.CountAsync());
    }

    [Fact]
    public async Task UploadLookup_IsScopedToTicketAndActor()
    {
        var upload = await CreateUploadAsync();
        await using var context = fixture.CreateContext();
        var store = new PostgresTicketAttachmentStore(context);
        Assert.NotNull(await store.GetUploadGrantAsync(upload.TicketId, upload.StorageKey, upload.ActorId));
        Assert.Null(await store.GetUploadGrantAsync(upload.TicketId, upload.StorageKey, Guid.NewGuid()));
        Assert.Null(await store.GetUploadGrantAsync(Guid.NewGuid(), upload.StorageKey, upload.ActorId));
    }

    [Fact]
    public async Task TicketLockTimeout_LeavesGrantAvailableForRetry()
    {
        var upload = await CreateUploadAsync();
        await using (var holder = fixture.CreateContext())
        {
            await using var transaction = await holder.Database.BeginTransactionAsync();
            await TicketRowLock.RequireOpenAsync(holder, upload.TicketId, "Phiếu đã kết thúc.", default);
            await using var confirmation = fixture.CreateContext();
            var error = await Assert.ThrowsAsync<AttachmentConfirmationBusyException>(() =>
                new PostgresTicketAttachmentStore(confirmation).ConfirmAsync(upload.Id, CreateAttachment(upload), DateTime.UtcNow));
            Assert.Equal(PostgresErrorCodes.LockNotAvailable, Assert.IsType<PostgresException>(error.InnerException).SqlState);
            Assert.Equal(1, error.RetryAfterSeconds);
        }

        await using var retry = fixture.CreateContext();
        Assert.False(await retry.TicketAttachments.AnyAsync(file => file.TicketId == upload.TicketId));
        var grant = await retry.AttachmentUploadGrants.AsNoTracking().SingleAsync(value => value.Id == upload.Id);
        Assert.Null(grant.ConsumedAt);
        var confirmed = await new PostgresTicketAttachmentStore(retry).ConfirmAsync(upload.Id, CreateAttachment(upload), DateTime.UtcNow);
        Assert.Equal(confirmed.Id, (await retry.TicketAttachments.SingleAsync(file => file.TicketId == upload.TicketId)).Id);
    }

    [Fact]
    public async Task ExpiredUpload_DoesNotPersistAttachment()
    {
        var upload = await CreateUploadAsync();
        await using var context = fixture.CreateContext();
        var attachment = TicketAttachment.Create(upload.TicketId, upload.FileName, upload.FileSizeBytes,
            upload.ContentType, $"tickets/{upload.TicketId}/attachments/{Guid.NewGuid()}", upload.ActorId, DateTime.UtcNow);
        await Assert.ThrowsAsync<ArgumentException>(() => new PostgresTicketAttachmentStore(context)
            .ConfirmAsync(upload.Id, attachment, upload.ExpiresAt));
        await using var verification = fixture.CreateContext();
        Assert.False(await verification.TicketAttachments.AnyAsync(value => value.TicketId == upload.TicketId));
    }

    [Fact]
    public async Task ActiveUploadQuota_IsEnforcedBeforeAddingAnotherIntent()
    {
        var upload = await CreateUploadAsync();
        await using (var setup = fixture.CreateContext())
        {
            setup.AttachmentUploadGrants.AddRange(Enumerable.Range(0, 19).Select(_ => AttachmentUploadGrant.CreatePending(
                upload.TicketId, upload.ActorId, "file.txt", 10, "text/plain", DateTime.UtcNow.AddHours(1))));
            await setup.SaveChangesAsync();
        }
        await using var context = fixture.CreateContext();
        var rejected = AttachmentUploadGrant.CreatePending(upload.TicketId, upload.ActorId, "file.txt", 10, "text/plain", DateTime.UtcNow.AddHours(1));
        await Assert.ThrowsAsync<ArgumentException>(() => new PostgresTicketAttachmentStore(context).CreateUploadGrantAsync(rejected));
        await using var verification = fixture.CreateContext();
        Assert.Equal(20, await verification.AttachmentUploadGrants.CountAsync(value => value.ActorId == upload.ActorId));
    }

    private static TicketAttachment CreateAttachment(AttachmentUploadGrant upload) => TicketAttachment.Create(
        upload.TicketId, upload.FileName, upload.FileSizeBytes, upload.ContentType,
        $"tickets/{upload.TicketId}/attachments/{Guid.NewGuid()}", upload.ActorId, DateTime.UtcNow);

    private async Task<AttachmentUploadGrant> CreateUploadAsync(long size = 10)
    {
        await using var context = fixture.CreateContext();
        var actor = Guid.NewGuid();
        var ticket = Ticket.Create(actor, "Lỗi VPN", "Mô tả đủ độ dài", TicketCategories.VPN, DateTime.UtcNow);
        await new PostgresTicketWriter(context).CreateAsync(ticket, null, default);
        var upload = AttachmentUploadGrant.CreatePending(ticket.Id, actor, "file.txt", size, "text/plain", DateTime.UtcNow.AddHours(1));
        await new PostgresTicketAttachmentStore(context).CreateUploadGrantAsync(upload);
        return upload;
    }
}
