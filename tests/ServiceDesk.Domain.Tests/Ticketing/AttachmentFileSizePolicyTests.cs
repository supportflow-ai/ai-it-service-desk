using ServiceDesk.Modules.Ticketing.Domain;
using Xunit;

namespace ServiceDesk.Domain.Tests.Ticketing;

public sealed class AttachmentFileSizePolicyTests
{
    [Theory]
    [InlineData(0L, false)]
    [InlineData(-1L, false)]
    [InlineData(10485760L, true)]
    [InlineData(10485761L, false)]
    public void AttachmentEnforcesTheApprovedPerFileLimit(long bytes, bool accepted)
    {
        TicketAttachment Create() => TicketAttachment.Create(Guid.NewGuid(), "file.pdf", bytes, "application/pdf", "private-key", Guid.NewGuid(), DateTime.UtcNow);
        if (accepted) Assert.Equal(bytes, Create().FileSizeBytes);
        else Assert.Throws<ArgumentException>(() => Create());
    }
}
