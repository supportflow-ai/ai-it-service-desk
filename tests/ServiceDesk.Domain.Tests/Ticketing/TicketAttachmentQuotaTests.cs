using ServiceDesk.Modules.Ticketing.Domain;
using Xunit;

namespace ServiceDesk.Domain.Tests.Ticketing;

public sealed class TicketAttachmentQuotaTests
{
    private const long MiB = 1024L * 1024;

    [Fact]
    public void AcceptsTenthFileWhenTotalIsExactlyFiftyMiB()
    {
        var attachment = Create(5 * MiB);

        attachment.RequireTicketCapacity(Enumerable.Repeat(5 * MiB, 9).ToArray());
    }

    [Fact]
    public void RejectsEleventhFileEvenWhenTotalFits()
    {
        var attachment = Create(1);

        Assert.Throws<ArgumentException>(() =>
            attachment.RequireTicketCapacity(Enumerable.Repeat(1L, 10).ToArray()));
    }

    [Fact]
    public void RejectsTotalExceedingFiftyMiBByOneByte()
    {
        var attachment = Create(10 * MiB);

        Assert.Throws<ArgumentException>(() => attachment.RequireTicketCapacity(
            [10 * MiB, 10 * MiB, 10 * MiB, 10 * MiB, 1]));
    }

    private static TicketAttachment Create(long size) => TicketAttachment.Create(
        Guid.NewGuid(), "log.txt", size, "text/plain", "attachments/test-log.txt", Guid.NewGuid(), DateTime.UtcNow);
}
