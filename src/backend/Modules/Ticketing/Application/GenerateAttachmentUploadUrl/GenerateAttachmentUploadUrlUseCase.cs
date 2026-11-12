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
            throw new InvalidInputException("attachment_file_name_required", "Cần có tên tệp.");

        var ext = Path.GetExtension(req.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            throw new InvalidInputException("attachment_type_not_allowed", $"Loại tệp '{ext}' không được phép. Các loại được phép: PDF, PNG, JPG, DOCX, TXT, ZIP.");

        if (req.FileSizeBytes <= 0 || req.FileSizeBytes > MaxSizeBytes)
            throw TicketErrors.FileSizeInvalid();

        var scope = TicketPermissions.ReadScopeFor(currentUser, actorId, includeOwner: true);

        var ticket = await ticketQueries.GetByIdAsync(command.TicketId, scope, cancellationToken);
        if (ticket == null) throw TicketErrors.NotFound();
        if (ticket.Status is (int)TicketStatus.Closed or (int)TicketStatus.Cancelled)
            throw TicketErrors.TicketClosed("Không thể tải tệp lên vé đã kết thúc.");

        const int expirySeconds = 3600;
        var upload = AttachmentUploadGrant.CreatePending(command.TicketId, actorId, Path.GetFileName(req.FileName),
            req.FileSizeBytes, req.ContentType, clock.UtcNow.UtcDateTime.AddSeconds(expirySeconds));

        try
        {
            var signed = await storageService.CreateUploadAsync(storageService.DefaultBucket, upload.StorageKey, upload.ContentType, upload.FileSizeBytes, expirySeconds, cancellationToken);
            await attachmentStore.CreateUploadGrantAsync(upload, cancellationToken);
            return new UploadUrlResult(signed.Url, upload.StorageKey, expirySeconds, signed.FormData);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ArgumentException and not DomainException)
        {
            throw new ServiceUnavailableException("Dịch vụ lưu trữ tệp hiện không khả dụng.");
        }
    }
}
