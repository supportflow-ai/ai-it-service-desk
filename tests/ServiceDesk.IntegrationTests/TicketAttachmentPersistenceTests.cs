using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.Modules.Ticketing.Infrastructure;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class TicketAttachmentPersistenceTests(TicketIdempotencyPostgresFixture fixture)
    : IClassFixture<TicketIdempotencyPostgresFixture>
{
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

    private async Task<AttachmentUploadGrant> CreateUploadAsync()
    {
        await using var context = fixture.CreateContext();
        var actor = Guid.NewGuid();
        var ticket = Ticket.Create(actor, "Lỗi VPN", "Mô tả đủ độ dài", TicketCategories.VPN, DateTime.UtcNow);
        await new PostgresTicketWriter(context).CreateAsync(ticket, null, default);
        var upload = AttachmentUploadGrant.CreatePending(ticket.Id, actor, "file.txt", 10, "text/plain", DateTime.UtcNow.AddHours(1));
        await new PostgresTicketAttachmentStore(context).CreateUploadGrantAsync(upload);
        return upload;
    }
}
