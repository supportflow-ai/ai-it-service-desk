using Moq;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.ConfirmAttachmentUpload;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.GenerateAttachmentUploadUrl;
using ServiceDesk.Modules.Ticketing.Application.GetAttachmentDownloadUrl;
using ServiceDesk.Modules.Ticketing.Application.GetTicketAttachments;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Ticketing.Domain;
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

    private void SetupTicketStatus(TicketStatus status)
    {
        var ticketDto = new TicketDto(_ticketId, "IT-2026-0001", "Title", "Description", "VPN", (int)status, _now.UtcDateTime, RequesterId: _userId);
        _ticketQueries.Setup(q => q.GetByIdAsync(_ticketId, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticketDto);
    }

    public TicketAttachmentUseCaseTests()
    {
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);
        _user.SetupGet(u => u.UserId).Returns(_userId.ToString());
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _storageService.SetupGet(s => s.DefaultBucket).Returns("servicedesk-test");

        var ticketDto = new TicketDto(_ticketId, "IT-2026-0001", "Title", "Description", "VPN", 1, _now.UtcDateTime, RequesterId: _userId);
        _ticketQueries.Setup(q => q.GetByIdAsync(_ticketId, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticketDto);
    }

    [Fact]
    public async Task DownloadUrl_InvisibleTicket_IsNotFoundBeforeStorageKeyValidation()
    {
        _ticketQueries.Setup(q => q.GetByIdAsync(_ticketId, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TicketDto?)null);
        var useCase = new GetAttachmentDownloadUrlUseCase(_ticketQueries.Object, _storageService.Object, _attachmentStore.Object, _user.Object);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(new(_ticketId, "tickets/other/secret.pdf")));

        _attachmentStore.VerifyNoOtherCalls();
        _storageService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DownloadUrl_WrongTicketPrefix_ThrowsForbidden()
    {
        var otherTicketId = Guid.NewGuid();
        var useCase = new GetAttachmentDownloadUrlUseCase(_ticketQueries.Object, _storageService.Object, _attachmentStore.Object, _user.Object);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            useCase.ExecuteAsync(new(_ticketId, $"tickets/{otherTicketId}/malicious.pdf")));
    }

    [Fact]
    public async Task DownloadUrl_MinIOThrows_ThrowsServiceUnavailable()
    {
        var validKey = $"tickets/{_ticketId}/document.pdf";
        _attachmentStore.Setup(s => s.ExistsAsync(_ticketId, validKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _storageService.Setup(s => s.GetPresignedDownloadUrlAsync("servicedesk-test", validKey, 3600, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("MinIO offline"));

        var useCase = new GetAttachmentDownloadUrlUseCase(_ticketQueries.Object, _storageService.Object, _attachmentStore.Object, _user.Object);

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            useCase.ExecuteAsync(new(_ticketId, validKey)));
    }

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task GenerateUpload_TerminalTicket_RejectsBeforeStorage(TicketStatus status)
    {
        SetupTicketStatus(status);
        var useCase = new GenerateAttachmentUploadUrlUseCase(_ticketQueries.Object, _storageService.Object,
            _attachmentStore.Object, _clock.Object, _user.Object);

        await Assert.ThrowsAsync<RuleViolationException>(() => useCase.ExecuteAsync(new(_ticketId, new("evidence.pdf", 1024, "application/pdf"))));

        _storageService.VerifyNoOtherCalls();
        _attachmentStore.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task ConfirmUpload_TerminalTicket_RejectsBeforeGrantLookup(TicketStatus status)
    {
        SetupTicketStatus(status);
        var useCase = new ConfirmAttachmentUploadUseCase(_ticketQueries.Object, _attachmentStore.Object,
            _storageService.Object, _user.Object, _clock.Object);

        await Assert.ThrowsAsync<RuleViolationException>(() => useCase.ExecuteAsync(new(_ticketId,
            new ConfirmAttachmentUploadRequest("evidence.png", 1024, "image/png", $"tickets/{_ticketId}/attachments/existing"))));

        _attachmentStore.VerifyNoOtherCalls();
        _storageService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ConfirmUpload_TerminalTicket_RejectsEvenWhenAlreadyConfirmed()
    {
        SetupTicketStatus(TicketStatus.Closed);
        var useCase = new ConfirmAttachmentUploadUseCase(_ticketQueries.Object, _attachmentStore.Object,
            _storageService.Object, _user.Object, _clock.Object);

        await Assert.ThrowsAsync<RuleViolationException>(() => useCase.ExecuteAsync(new(_ticketId,
            new ConfirmAttachmentUploadRequest("evidence.png", 1024, "image/png", $"tickets/{_ticketId}/attachments/existing"))));

        _attachmentStore.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ConfirmUpload_ValidRequest_SavesMetadata()
    {
        var upload = AttachmentUploadGrant.CreatePending(_ticketId, _userId, "evidence.png", 1024, "image/png", _now.UtcDateTime.AddHours(1));
        var validKey = upload.StorageKey;
        _attachmentStore.Setup(s => s.GetUploadGrantAsync(_ticketId, validKey, _userId, It.IsAny<CancellationToken>())).ReturnsAsync(upload);
        _storageService.Setup(s => s.StatAsync("servicedesk-test", It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new StoredObject(1024, "image/png", "etag"));
        var request = new ConfirmAttachmentUploadRequest("evidence.png", 1024, "image/png", validKey);

        _attachmentStore.Setup(s => s.ConfirmAsync(upload.Id, It.IsAny<TicketAttachment>(), _now.UtcDateTime, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, TicketAttachment attachment, DateTime _, CancellationToken _) =>
                new TicketAttachmentDto(attachment.Id, _ticketId, "evidence.png", 1024, "image/png", attachment.StorageKey, _userId, _now.UtcDateTime));

        var useCase = new ConfirmAttachmentUploadUseCase(_ticketQueries.Object, _attachmentStore.Object, _storageService.Object, _user.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new(_ticketId, request));

        Assert.Equal("evidence.png", result.FileName);
        Assert.StartsWith($"tickets/{_ticketId}/attachments/", result.StorageKey);
        _attachmentStore.VerifyAll();
    }

    [Fact]
    public async Task ConfirmUpload_UnissuedStorageKey_Throws()
    {
        var key = $"tickets/{_ticketId}/unissued.png";
        var useCase = new ConfirmAttachmentUploadUseCase(_ticketQueries.Object, _attachmentStore.Object, _storageService.Object, _user.Object, _clock.Object);
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(new(_ticketId, new("evidence.png", 1024, "image/png", key))));
        _storageService.Verify(s => s.StatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmUpload_StorageMetadataMismatch_DoesNotConfirm()
    {
        var grant = AttachmentUploadGrant.CreatePending(_ticketId, _userId, "evidence.png", 1024, "image/png", _now.UtcDateTime.AddHours(1));
        _attachmentStore.Setup(s => s.GetUploadGrantAsync(_ticketId, grant.StorageKey, _userId, It.IsAny<CancellationToken>())).ReturnsAsync(grant);
        _storageService.Setup(s => s.StatAsync("servicedesk-test", grant.StorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredObject(2048, "image/png", "etag"));
        var useCase = new ConfirmAttachmentUploadUseCase(_ticketQueries.Object, _attachmentStore.Object, _storageService.Object, _user.Object, _clock.Object);
        await Assert.ThrowsAsync<RuleViolationException>(() => useCase.ExecuteAsync(new(_ticketId, new("evidence.png", 1024, "image/png", grant.StorageKey))));
        _attachmentStore.Verify(s => s.ConfirmAsync(It.IsAny<Guid>(), It.IsAny<TicketAttachment>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmUpload_MissingObject_DoesNotSaveMetadata()
    {
        var grant = AttachmentUploadGrant.CreatePending(_ticketId, _userId, "evidence.png", 1024, "image/png", _now.UtcDateTime.AddHours(1));
        _attachmentStore.Setup(s => s.GetUploadGrantAsync(_ticketId, grant.StorageKey, _userId, It.IsAny<CancellationToken>())).ReturnsAsync(grant);
        _storageService.Setup(s => s.StatAsync("servicedesk-test", grant.StorageKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Sensitive storage details"));
        var useCase = new ConfirmAttachmentUploadUseCase(_ticketQueries.Object, _attachmentStore.Object, _storageService.Object, _user.Object, _clock.Object);
        var error = await Assert.ThrowsAsync<ServiceUnavailableException>(() => useCase.ExecuteAsync(new(_ticketId, new("evidence.png", 1024, "image/png", grant.StorageKey))));
        Assert.DoesNotContain("Sensitive storage details", error.Message);
        _attachmentStore.Verify(s => s.ConfirmAsync(It.IsAny<Guid>(), It.IsAny<TicketAttachment>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateUpload_IssuesCanonicalGrantAndBoundedPostPolicy()
    {
        AttachmentUploadGrant? issued = null;
        var fields = new Dictionary<string, string> { ["policy"] = "synthetic-policy" };
        _storageService.Setup(s => s.CreateUploadAsync("servicedesk-test", It.IsAny<string>(), "text/plain", 5, 3600, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PresignedUpload("https://storage.example.invalid/bucket", fields));
        _attachmentStore.Setup(s => s.CreateUploadGrantAsync(It.IsAny<AttachmentUploadGrant>(), It.IsAny<CancellationToken>()))
            .Callback<AttachmentUploadGrant, CancellationToken>((grant, _) => issued = grant)
            .Returns(Task.CompletedTask);
        var useCase = new GenerateAttachmentUploadUrlUseCase(_ticketQueries.Object, _storageService.Object,
            _attachmentStore.Object, _clock.Object, _user.Object);
        var result = await useCase.ExecuteAsync(new(_ticketId, new("log.txt", 5, "text/plain")));
        Assert.NotNull(issued);
        Assert.Equal(_userId, issued.ActorId);
        Assert.StartsWith($"uploads/pending/{_ticketId}/", issued.StorageKey);
        Assert.Equal(issued.StorageKey, result.StorageKey);
        Assert.Equal(_now.UtcDateTime.AddHours(1), issued.ExpiresAt);
        Assert.Same(fields, result.FormData);
        _attachmentStore.Verify(s => s.CreateUploadGrantAsync(It.IsAny<AttachmentUploadGrant>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
