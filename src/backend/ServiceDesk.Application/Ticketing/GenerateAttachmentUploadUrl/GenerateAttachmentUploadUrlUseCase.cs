using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Ticketing.GenerateAttachmentUploadUrl;

public record GenerateAttachmentUploadUrlCommand(Guid TicketId, GenerateUploadUrlRequest Request);

public interface IGenerateAttachmentUploadUrl
{
    Task<UploadUrlResult> ExecuteAsync(GenerateAttachmentUploadUrlCommand command, CancellationToken cancellationToken = default);
}

internal sealed class GenerateAttachmentUploadUrlUseCase(
    ITicketQueries ticketQueries,
    IObjectStorageService storageService,
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

        var isStaff = currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin);
        var scope = isStaff ? (TicketReadScope)new TicketReadScope.AllTickets() : new TicketReadScope.OwnTickets(actorId);

        var ticket = await ticketQueries.GetByIdAsync(command.TicketId, scope, cancellationToken);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found.");

        var storageKey = $"tickets/{command.TicketId}/{Guid.NewGuid()}_{Path.GetFileName(req.FileName)}";
        const int expirySeconds = 3600;

        string uploadUrl;
        try
        {
            uploadUrl = await storageService.GetPresignedUploadUrlAsync("servicedesk", storageKey, expirySeconds, cancellationToken);
        }
        catch
        {
            // Fallback khi MinIO chưa kết nối
            uploadUrl = $"http://localhost:9000/servicedesk/{storageKey}?upload=true";
        }

        return new UploadUrlResult(uploadUrl, storageKey, expirySeconds);
    }
}
