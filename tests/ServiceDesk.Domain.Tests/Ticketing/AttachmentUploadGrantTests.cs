using ServiceDesk.Modules.Ticketing.Domain;
using Xunit;

namespace ServiceDesk.Domain.Tests.Ticketing;

public sealed class AttachmentUploadGrantTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ConfirmLinksAttachmentAndConsumesGrantTogether()
    {
        var grant = Grant();
        var attachment = Attachment(grant);
        grant.Confirm(attachment, Now);
        Assert.Equal(attachment.Id, grant.AttachmentId);
        Assert.Equal(Now, grant.ConsumedAt);
        Assert.Throws<ArgumentException>(() => grant.Confirm(Attachment(grant), Now));
        Assert.Equal(attachment.Id, grant.AttachmentId);
    }

    [Fact]
    public void ExpiryBoundaryRejectsWithoutChangingGrant()
    {
        var grant = Grant();
        Assert.Throws<ArgumentException>(() => grant.Confirm(Attachment(grant), grant.ExpiresAt));
        Assert.Null(grant.AttachmentId);
        Assert.Null(grant.ConsumedAt);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("ticket")]
    [InlineData("metadata")]
    [InlineData("mutable-key")]
    public void RejectsAttachmentOutsideGrant(string mismatch)
    {
        var grant = Grant();
        var attachment = TicketAttachment.Create(mismatch == "ticket" ? Guid.NewGuid() : grant.TicketId,
            mismatch == "metadata" ? "other.txt" : grant.FileName, grant.FileSizeBytes, grant.ContentType,
            mismatch == "mutable-key" ? grant.StorageKey : $"tickets/{grant.TicketId}/attachments/{Guid.NewGuid()}",
            mismatch == "actor" ? Guid.NewGuid() : grant.ActorId, Now);
        Assert.Throws<ArgumentException>(() => grant.Confirm(attachment, Now));
        Assert.Null(grant.AttachmentId);
        Assert.Null(grant.ConsumedAt);
    }

    [Fact]
    public void LegacyConsumedGrantCannotBePromotedAsAnotherAttachment()
    {
        var grant = Grant();
        grant.Consume(Now);
        Assert.Throws<ArgumentException>(() => grant.Confirm(Attachment(grant), Now));
        Assert.Null(grant.AttachmentId);
        Assert.Equal(Now, grant.ConsumedAt);
    }

    private static AttachmentUploadGrant Grant() => AttachmentUploadGrant.CreatePending(Guid.NewGuid(), Guid.NewGuid(),
        "file.txt", 10, "text/plain", Now.AddHours(1));
    private static TicketAttachment Attachment(AttachmentUploadGrant grant) => TicketAttachment.Create(grant.TicketId,
        grant.FileName, grant.FileSizeBytes, grant.ContentType, $"tickets/{grant.TicketId}/attachments/{Guid.NewGuid()}", grant.ActorId, Now);
}
