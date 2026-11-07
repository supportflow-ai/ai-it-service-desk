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
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".docx", ".txt", ".zip"
    };

    private const long MaxSizeBytes = 10 * 1024 * 1024; // 10MB

    public async Task<UploadUrlResult> ExecuteAsync(GenerateAttachmentUploadUrlCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(command);
        var req = command.Request;

        if (string.IsNullOrWhiteSpace(req.FileName))
            throw new ArgumentException("File name is required.");

        var ext = Path.GetExtension(req.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            throw new ArgumentException($"File type '{ext}' is not allowed. Allowed types: PDF, PNG, JPG, DOCX, TXT, ZIP.");

        if (req.FileSizeBytes <= 0 || req.FileSizeBytes > MaxSizeBytes)
            throw new ArgumentException("File size must be between 1 byte and 10MB.");

        var isStaff = TicketPermissions.IsStaff(currentUser);
        var scope = isStaff ? (TicketReadScope)new TicketReadScope.AllTickets() : new TicketReadScope.OwnTickets(actorId);

        var ticket = await ticketQueries.GetByIdAsync(command.TicketId, scope, cancellationToken);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found.");
        if (ticket.Status is (int)TicketStatus.Closed or (int)TicketStatus.Cancelled)
            throw new ArgumentException("Không thể tải tệp lên vé đã kết thúc.");

        const int expirySeconds = 3600;
        var upload = AttachmentUploadGrant.CreatePending(command.TicketId, actorId, Path.GetFileName(req.FileName),
            req.FileSizeBytes, req.ContentType, clock.UtcNow.UtcDateTime.AddSeconds(expirySeconds));

        try
        {
            var signed = await storageService.CreateUploadAsync(storageService.DefaultBucket, upload.StorageKey, upload.ContentType, upload.FileSizeBytes, expirySeconds, cancellationToken);
            await attachmentStore.CreateUploadGrantAsync(upload, cancellationToken);
            return new UploadUrlResult(signed.Url, upload.StorageKey, expirySeconds, signed.FormData);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ArgumentException)
        {
            throw new ServiceUnavailableException("Dịch vụ lưu trữ tệp hiện không khả dụng.");
        }
    }
}
