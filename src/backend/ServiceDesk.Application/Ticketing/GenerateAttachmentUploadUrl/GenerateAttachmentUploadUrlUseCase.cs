using ServiceDesk.Application.Common;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.Ticketing.GenerateAttachmentUploadUrl;

public record GenerateAttachmentUploadUrlCommand(Guid TicketId, GenerateUploadUrlRequest Request);

public interface IGenerateAttachmentUploadUrl
{
    Task<UploadUrlResult> ExecuteAsync(GenerateAttachmentUploadUrlCommand command, CancellationToken cancellationToken = default);
}

internal sealed class GenerateAttachmentUploadUrlUseCase(
    ITicketQueries ticketQueries,
    ITicketAttachmentStore attachmentStore,
    IObjectStorageService storageService,
    ICurrentUser currentUser,
    IClock clock) : IGenerateAttachmentUploadUrl
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

        var isStaff = currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin);
        var scope = isStaff ? (TicketReadScope)new TicketReadScope.AllTickets() : new TicketReadScope.OwnTickets(actorId);

        var ticket = await ticketQueries.GetByIdAsync(command.TicketId, scope, cancellationToken);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found.");

        var storageKey = $"tickets/{command.TicketId}/{Guid.NewGuid()}_{Path.GetFileName(req.FileName)}";
        const int expirySeconds = 3600;
        var expiresAt = clock.UtcNow.AddSeconds(expirySeconds).UtcDateTime;

        try
        {
            var uploadUrl = await storageService.GetPresignedUploadUrlAsync("servicedesk", storageKey, expirySeconds, cancellationToken);
            await attachmentStore.CreateUploadGrantAsync(
                AttachmentUploadGrant.Create(command.TicketId, actorId, req.FileName, req.FileSizeBytes, req.ContentType, storageKey, expiresAt),
                cancellationToken);
            return new UploadUrlResult(uploadUrl, storageKey, expirySeconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            throw new ServiceUnavailableException("Dịch vụ lưu trữ tệp (MinIO) hiện không khả dụng.");
        }
    }
}
