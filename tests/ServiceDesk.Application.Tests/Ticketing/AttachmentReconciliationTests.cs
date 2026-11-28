using Moq;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Application.ReconcileAttachments;
using ServiceDesk.SharedKernel.Application.Abstractions;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public sealed class AttachmentReconciliationTests
{
    [Fact]
    public async Task OnlyOldObjectsApprovedByAuthoritativeStoreAreDeletedAndFailuresAreRetryable()
    {
        var now = new DateTime(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc);
        var storage = new Mock<IObjectStorageService>(MockBehavior.Strict);
        var store = new Mock<IAttachmentReconciliationStore>(MockBehavior.Strict);
        storage.SetupGet(s => s.DefaultBucket).Returns("private"); storage.SetupGet(s => s.DownloadExpirySeconds).Returns(300);
        store.Setup(s => s.GetUtcNowAsync(It.IsAny<CancellationToken>())).ReturnsAsync(now);
        storage.Setup(s => s.ListAsync("private", "tickets/", "cursor", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageObjectPage(new[] { new StorageObjectEntry("referenced", now.AddDays(-2)), new("active", now.AddDays(-2)), new("old", now.AddDays(-2)), new("retry", now.AddDays(-2)), new("recent", now) }, "next"));
        store.Setup(s => s.MayDeleteObjectAsync("referenced", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        store.Setup(s => s.MayDeleteObjectAsync("active", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        foreach (var key in new[] { "old", "retry" }) store.Setup(s => s.MayDeleteObjectAsync(key, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        storage.Setup(s => s.DeleteAsync("private", "old", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        storage.Setup(s => s.DeleteAsync("private", "retry", It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("provider failure"));
        var result = await new ReconcileAttachmentsUseCase(store.Object, storage.Object).ExecuteAsync(new("tickets/", "cursor", TimeSpan.FromDays(1)));
        Assert.Equal(new AttachmentReconciliationResult(5, 1, 1, 0, "next"), result);
        storage.VerifyAll(); store.VerifyAll();
    }

    [Fact]
    public async Task UnsafeRetentionOrPrefixNeverListsOrDeletesObjects()
    {
        var storage = new Mock<IObjectStorageService>(); storage.SetupGet(s => s.DownloadExpirySeconds).Returns(300);
        var store = new Mock<IAttachmentReconciliationStore>();
        var useCase = new ReconcileAttachmentsUseCase(store.Object, storage.Object);
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(new("tickets/", null, TimeSpan.FromSeconds(300))));
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(new("unowned/", null, TimeSpan.FromDays(1))));
        storage.Verify(s => s.ListAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        store.VerifyNoOtherCalls();
    }
}
