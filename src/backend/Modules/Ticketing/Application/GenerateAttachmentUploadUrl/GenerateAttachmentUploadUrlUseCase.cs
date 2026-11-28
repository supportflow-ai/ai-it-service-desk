using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.GenerateAttachmentUploadUrl;

public record GenerateAttachmentUploadUrlCommand(Guid TicketId, GenerateUploadUrlRequest Request);

public interface IGenerateAttachmentUploadUrl
{
    Task<UploadUrlResult> ExecuteAsync(GenerateAttachmentUploadUrlCommand command, CancellationToken cancellationToken = default);
}

internal sealed class GenerateAttachmentUploadUrlUseCase(
    ITicketQueries ticketQueries,
    IObjectStorageService storageService,
    ITicketAttachmentStore attachmentStore,
    IClock clock,
    ICurrentUser currentUser) : IGenerateAttachmentUploadUrl
{
    public async Task<UploadUrlResult> ExecuteAsync(GenerateAttachmentUploadUrlCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(command);
        var req = command.Request;

        var normalizedContentType = AttachmentPolicy.ValidateMetadata(req.FileName, req.FileSizeBytes, req.ContentType);

        var scope = TicketPermissions.ReadScopeFor(currentUser, actorId, includeOwner: true);

        var ticket = await ticketQueries.GetByIdAsync(command.TicketId, scope, cancellationToken);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found.");
        if (ticket.Status is (int)TicketStatus.Closed or (int)TicketStatus.Cancelled)
            throw new ArgumentException("Không thể tải tệp lên vé đã kết thúc.");

        var expirySeconds = storageService.UploadExpirySeconds;
        var upload = AttachmentUploadGrant.CreatePending(command.TicketId, actorId, Path.GetFileName(req.FileName),
            req.FileSizeBytes, normalizedContentType, clock.UtcNow.UtcDateTime.AddSeconds(expirySeconds));

        try
        {
            var signed = await storageService.CreateUploadAsync(storageService.DefaultBucket, upload.StorageKey, upload.ContentType, upload.FileSizeBytes, upload.ExpiresAt, cancellationToken);
            await attachmentStore.CreateUploadGrantAsync(upload, cancellationToken);
            return new UploadUrlResult(signed.Url, upload.StorageKey, expirySeconds, signed.FormData, signed.RequiredHeaders);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ArgumentException)
        {
            throw new ServiceUnavailableException("Dịch vụ lưu trữ tệp hiện không khả dụng.");
        }
    }

}
