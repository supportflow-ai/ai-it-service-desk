using Moq;
using ServiceDesk.Application.Common;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.ConfirmAttachmentUpload;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.GetAttachmentDownloadUrl;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Ticketing;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public sealed class AttachmentConfirmationSecurityTests
{
    private readonly Mock<ITicketQueries> _tickets = new();
    private readonly Mock<ITicketAttachmentStore> _store = new(MockBehavior.Strict);
    private readonly Mock<IObjectStorageService> _storage = new(MockBehavior.Strict);
    private readonly Mock<ICurrentUser> _actor = new();
    private readonly Mock<IClock> _clock = new();
    private readonly DateTime _now = new(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc);
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _ticket = Guid.NewGuid();
    private readonly AttachmentUploadGrant _upload;

    public AttachmentConfirmationSecurityTests()
    {
        _actor.SetupGet(value => value.UserId).Returns(_user.ToString());
        _actor.SetupGet(value => value.IsAuthenticated).Returns(true);
        _actor.SetupGet(value => value.Roles).Returns([RoleNames.Requester]);
        _clock.SetupGet(value => value.UtcNow).Returns(new DateTimeOffset(_now));
        _storage.SetupGet(value => value.DefaultBucket).Returns("configured-bucket");
        _upload = AttachmentUploadGrant.CreatePending(_ticket, _user, "image.png", 1024, "image/png", _now.AddHours(1));
        _tickets.Setup(value => value.GetByIdAsync(_ticket, It.Is<TicketReadScope>(scope => scope is TicketReadScope.OwnTickets), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDto(_ticket, "IT-2026-0001", "VPN", "Description", "VPN", 1, _now));
        _store.Setup(value => value.GetUploadGrantAsync(_ticket, _upload.StorageKey, _user, It.IsAny<CancellationToken>())).ReturnsAsync(_upload);
    }

    [Theory]
    [InlineData(0, "image/png")]
    [InlineData(2048, "image/png")]
    [InlineData(1024, "text/html")]
    public async Task MissingOrMismatchedObject_CannotBeConfirmed(long size, string type)
    {
        _storage.Setup(value => value.StatAsync("configured-bucket", _upload.StorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(size == 0 ? null : new StoredObject(size, type, "etag"));
        await Assert.ThrowsAsync<ArgumentException>(() => Confirm().ExecuteAsync(new(_ticket, Request())));
        _store.Verify(value => value.ConfirmAsync(It.IsAny<Guid>(), It.IsAny<TicketAttachment>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MetadataCannotBeReplacedAtConfirmation()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Confirm().ExecuteAsync(new(_ticket, Request() with { FileSizeBytes = 2048 })));
        _storage.Verify(value => value.StatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExpiredUpload_IsRejectedBeforeStorage()
    {
        _clock.SetupGet(value => value.UtcNow).Returns(new DateTimeOffset(_now.AddHours(2)));
        await Assert.ThrowsAsync<ArgumentException>(() => Confirm().ExecuteAsync(new(_ticket, Request())));
        _storage.Verify(value => value.StatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnknownUpload_IsNotFoundBeforeStorage()
    {
        _store.Setup(value => value.GetUploadGrantAsync(_ticket, _upload.StorageKey, _user, It.IsAny<CancellationToken>())).ReturnsAsync((AttachmentUploadGrant?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Confirm().ExecuteAsync(new(_ticket, Request())));
        _storage.Verify(value => value.StatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VerifiedCopy_UsesServerMetadataAndPrivateFinalKey()
    {
        _storage.Setup(value => value.StatAsync("configured-bucket", It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new StoredObject(1024, "image/png", "etag"));
        _storage.Setup(value => value.CopyAsync("configured-bucket", _upload.StorageKey, It.IsAny<string>(), "etag", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _storage.Setup(value => value.DeleteAsync("configured-bucket", _upload.StorageKey, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _store.Setup(value => value.ConfirmAsync(_upload.Id, It.IsAny<TicketAttachment>(), _now, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, TicketAttachment attachment, DateTime _, CancellationToken _) => new TicketAttachmentDto(attachment.Id,
                attachment.TicketId, attachment.FileName, attachment.FileSizeBytes, attachment.ContentType, attachment.StorageKey, attachment.UploadedBy, attachment.CreatedAt));
        var result = await Confirm().ExecuteAsync(new(_ticket, Request()));
        Assert.StartsWith($"tickets/{_ticket}/attachments/", result.StorageKey);
        Assert.NotEqual(_upload.StorageKey, result.StorageKey);
        Assert.Equal(_user, result.UploadedBy);
        _storage.Verify(value => value.CopyAsync("configured-bucket", _upload.StorageKey, result.StorageKey, "etag", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RepeatedConfirmation_ReturnsExistingAttachmentWithoutCopy()
    {
        var attachment = TicketAttachment.Create(_ticket, "image.png", 1024, "image/png", $"tickets/{_ticket}/attachments/{Guid.NewGuid()}", _user, _now);
        _upload.Confirm(attachment, _now);
        var expected = new TicketAttachmentDto(attachment.Id, _ticket, "image.png", 1024, "image/png", attachment.StorageKey, _user, _now);
        _store.Setup(value => value.GetByIdAsync(attachment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        Assert.Equal(expected, await Confirm().ExecuteAsync(new(_ticket, Request())));
        _storage.Verify(value => value.StatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StorageFailure_DoesNotExposeProviderDetails()
    {
        _storage.Setup(value => value.StatAsync("configured-bucket", _upload.StorageKey, It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("private-provider-details"));
        var error = await Assert.ThrowsAsync<ServiceUnavailableException>(() => Confirm().ExecuteAsync(new(_ticket, Request())));
        Assert.DoesNotContain("private-provider-details", error.Message);
    }

    [Fact]
    public async Task LegacyConsumedGrantWithoutLink_IsRejectedWithoutStorageAccess()
    {
        _upload.Consume(_now);
        await Assert.ThrowsAsync<ArgumentException>(() => Confirm().ExecuteAsync(new(_ticket, Request())));
        _storage.Verify(value => value.StatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LegacyPendingPutGrant_IsPromotedToPrivateFinalKey()
    {
        var legacy = AttachmentUploadGrant.Create(_ticket, _user, "image.png", 1024, "image/png",
            $"tickets/{_ticket}/{Guid.NewGuid()}_image.png", _now.AddHours(1));
        _store.Setup(value => value.GetUploadGrantAsync(_ticket, legacy.StorageKey, _user, It.IsAny<CancellationToken>())).ReturnsAsync(legacy);
        _storage.Setup(value => value.StatAsync("configured-bucket", It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new StoredObject(1024, "image/png", "legacy-etag"));
        _storage.Setup(value => value.CopyAsync("configured-bucket", legacy.StorageKey, It.IsAny<string>(), "legacy-etag", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _storage.Setup(value => value.DeleteAsync("configured-bucket", legacy.StorageKey, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _store.Setup(value => value.ConfirmAsync(legacy.Id, It.IsAny<TicketAttachment>(), _now, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, TicketAttachment attachment, DateTime _, CancellationToken _) => new TicketAttachmentDto(attachment.Id,
                attachment.TicketId, attachment.FileName, attachment.FileSizeBytes, attachment.ContentType, attachment.StorageKey, attachment.UploadedBy, attachment.CreatedAt));
        var result = await Confirm().ExecuteAsync(new(_ticket, Request() with { StorageKey = legacy.StorageKey }));
        Assert.StartsWith($"tickets/{_ticket}/attachments/", result.StorageKey);
        Assert.NotEqual(legacy.StorageKey, result.StorageKey);
        _storage.Verify(value => value.CopyAsync("configured-bucket", legacy.StorageKey, result.StorageKey, "legacy-etag", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DownloadRequiresConfirmedMetadata()
    {
        var key = $"tickets/{_ticket}/unconfirmed";
        _store.Setup(value => value.ExistsAsync(_ticket, key, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var useCase = new GetAttachmentDownloadUrlUseCase(_tickets.Object, _storage.Object, _store.Object, _actor.Object);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => useCase.ExecuteAsync(new(_ticket, key)));
        _storage.Verify(value => value.GetPresignedDownloadUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private ConfirmAttachmentUploadRequest Request() => new("image.png", 1024, "image/png", _upload.StorageKey);
    private ConfirmAttachmentUploadUseCase Confirm() => new(_tickets.Object, _store.Object, _storage.Object, _actor.Object, _clock.Object);
}
