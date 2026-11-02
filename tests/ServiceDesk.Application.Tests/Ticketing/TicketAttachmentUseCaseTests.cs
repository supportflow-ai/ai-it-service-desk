using Moq;
using ServiceDesk.Application.Common;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.ConfirmAttachmentUpload;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.GenerateAttachmentUploadUrl;
using ServiceDesk.Application.Ticketing.GetAttachmentDownloadUrl;
using ServiceDesk.Application.Ticketing.GetTicketAttachments;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Ticketing;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public sealed class TicketAttachmentUseCaseTests
{
    private readonly Mock<ITicketQueries> _ticketQueries = new();
    private readonly Mock<ITicketAttachmentStore> _attachmentStore = new();
    private readonly Mock<IObjectStorageService> _storageService = new();
    private readonly Mock<ICurrentUser> _user = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Guid _ticketId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

    public TicketAttachmentUseCaseTests()
    {
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);
        _user.SetupGet(u => u.UserId).Returns(_userId.ToString());
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _clock.SetupGet(c => c.UtcNow).Returns(_now);

        var ticketDto = new TicketDto(_ticketId, "IT-2026-0001", "Title", "Description", "VPN", 1, _now.UtcDateTime, RequesterId: _userId);
        _ticketQueries.Setup(q => q.GetByIdAsync(_ticketId, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticketDto);
    }

    [Fact]
    public async Task DownloadUrl_WrongTicketPrefix_ThrowsForbidden()
    {
        var otherTicketId = Guid.NewGuid();
        var useCase = new GetAttachmentDownloadUrlUseCase(_ticketQueries.Object, _storageService.Object, _user.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            useCase.ExecuteAsync(new(_ticketId, $"tickets/{otherTicketId}/malicious.pdf")));
    }

    [Fact]
    public async Task DownloadUrl_MinIOThrows_ThrowsServiceUnavailable()
    {
        var validKey = $"tickets/{_ticketId}/document.pdf";
        _storageService.Setup(s => s.GetPresignedDownloadUrlAsync("servicedesk", validKey, 3600, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("MinIO offline"));

        var useCase = new GetAttachmentDownloadUrlUseCase(_ticketQueries.Object, _storageService.Object, _user.Object);

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            useCase.ExecuteAsync(new(_ticketId, validKey)));
    }

    [Fact]
    public async Task ConfirmUpload_UnissuedStorageKey_Throws()
    {
        var storageKey = $"tickets/{_ticketId}/unissued.png";
        var request = new ConfirmAttachmentUploadRequest("evidence.png", 1024, "image/png", storageKey);
        _storageService.Setup(s => s.StatObjectAsync("servicedesk", storageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ObjectStorageObjectInfo(1024, "image/png"));
        _attachmentStore.Setup(s => s.ConfirmUploadAsync(_ticketId, _userId, request,
                It.IsAny<ObjectStorageObjectInfo>(), _now.UtcDateTime, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException());
        var useCase = new ConfirmAttachmentUploadUseCase(_ticketQueries.Object, _attachmentStore.Object, _storageService.Object, _user.Object, _clock.Object);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(new(_ticketId, request)));
    }

    [Fact]
    public async Task ConfirmUpload_StorageMetadataMismatch_DoesNotConfirm()
    {
        var validKey = $"tickets/{_ticketId}/evidence.png";
        var request = new ConfirmAttachmentUploadRequest("evidence.png", 1024, "image/png", validKey);
        _storageService.Setup(s => s.StatObjectAsync("servicedesk", validKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ObjectStorageObjectInfo(2048, "image/png"));
        _attachmentStore.Setup(s => s.ConfirmUploadAsync(_ticketId, _userId, request,
                It.IsAny<ObjectStorageObjectInfo>(), _now.UtcDateTime, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException());

        var useCase = new ConfirmAttachmentUploadUseCase(_ticketQueries.Object, _attachmentStore.Object, _storageService.Object, _user.Object, _clock.Object);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(new(_ticketId, request)));
    }

    [Fact]
    public async Task ConfirmUpload_MissingObject_DoesNotSaveMetadata()
    {
        var key = $"tickets/{_ticketId}/missing.png";
        _storageService.Setup(storage => storage.StatObjectAsync("servicedesk", key, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Sensitive storage details"));
        var useCase = new ConfirmAttachmentUploadUseCase(_ticketQueries.Object, _attachmentStore.Object, _storageService.Object, _user.Object, _clock.Object);

        var error = await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            useCase.ExecuteAsync(new(_ticketId, new("missing.png", 1024, "image/png", key))));

        Assert.DoesNotContain("Sensitive storage details", error.Message);
        _attachmentStore.Verify(store => store.ConfirmUploadAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<ConfirmAttachmentUploadRequest>(), It.IsAny<ObjectStorageObjectInfo>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmUpload_ValidRequest_SavesMetadata()
    {
        var validKey = $"tickets/{_ticketId}/evidence.png";
        var request = new ConfirmAttachmentUploadRequest("evidence.png", 1024, "image/png", validKey);
        _storageService.Setup(s => s.StatObjectAsync("servicedesk", validKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ObjectStorageObjectInfo(1024, "image/png"));
        _attachmentStore.Setup(s => s.ConfirmUploadAsync(_ticketId, _userId, request,
                It.IsAny<ObjectStorageObjectInfo>(), _now.UtcDateTime, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketAttachmentDto(Guid.NewGuid(), _ticketId, "evidence.png", 1024, "image/png", validKey, _userId, _now.UtcDateTime));

        var useCase = new ConfirmAttachmentUploadUseCase(_ticketQueries.Object, _attachmentStore.Object, _storageService.Object, _user.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new(_ticketId, request));

        Assert.Equal("evidence.png", result.FileName);
        _attachmentStore.VerifyAll();
    }
}
