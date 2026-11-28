using ServiceDesk.Modules.Ticketing.Domain;
using Xunit;

namespace ServiceDesk.Domain.Tests;

public sealed class AttachmentConfirmationLeaseTests
{
    [Fact]
    public void ExpiryAndReplacementFenceTheOldOwnerWithoutWaiting()
    {
        var now = new DateTime(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc);
        var grant = AttachmentUploadGrant.CreatePending(Guid.NewGuid(), Guid.NewGuid(), "file.txt", 5, "text/plain", now.AddMinutes(15));
        var old = Guid.NewGuid(); var next = Guid.NewGuid();
        Assert.True(grant.TryReserveConfirmation(old, now));
        var key = grant.ConfirmationStorageKey();
        Assert.False(grant.TryReserveConfirmation(next, now.AddMinutes(2).AddTicks(-1)));
        grant.RequireActiveConfirmation(key, now.AddMinutes(2).AddTicks(-1));
        Assert.Throws<ArgumentException>(() => grant.RequireActiveConfirmation(key, now.AddMinutes(2)));
        Assert.True(grant.TryReserveConfirmation(next, now.AddMinutes(2)));
        Assert.NotEqual(key, grant.ConfirmationStorageKey());
        Assert.Throws<ArgumentException>(() => grant.RequireActiveConfirmation(key, now.AddMinutes(2)));
        grant.ReleaseConfirmation(old);
        Assert.Equal(next, grant.ConfirmationAttemptId);
        grant.ReleaseConfirmation(next);
        Assert.Null(grant.ConfirmationAttemptId);
        Assert.Null(grant.ConfirmationLeaseUntil);
        Assert.Throws<ArgumentException>(() => grant.TryReserveConfirmation(Guid.NewGuid(), grant.ExpiresAt));
    }
}
