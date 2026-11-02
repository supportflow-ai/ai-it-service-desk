using ServiceDesk.Application.Common;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.Ticketing.ConfirmAttachmentUpload;

public record ConfirmAttachmentUploadCommand(Guid TicketId, ConfirmAttachmentUploadRequest Request);

public interface IConfirmAttachmentUpload
{
    Task<TicketAttachmentDto> ExecuteAsync(ConfirmAttachmentUploadCommand command, CancellationToken cancellationToken = default);
}

internal sealed class ConfirmAttachmentUploadUseCase(
    ITicketQueries ticketQueries,
    ITicketAttachmentStore attachmentStore,
    IObjectStorageService storageService,
    ICurrentUser currentUser,
    IClock clock) : IConfirmAttachmentUpload
{
    public async Task<TicketAttachmentDto> ExecuteAsync(ConfirmAttachmentUploadCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(command);
        var req = command.Request;

        var expectedPrefix = $"tickets/{command.TicketId}/";
        if (string.IsNullOrWhiteSpace(req.StorageKey) || !req.StorageKey.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Storage key không hợp lệ hoặc không thuộc về vé này.");
        }

        var isStaff = currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin);
        var scope = isStaff ? (TicketReadScope)new TicketReadScope.AllTickets() : new TicketReadScope.OwnTickets(actorId);

        var ticket = await ticketQueries.GetByIdAsync(command.TicketId, scope, cancellationToken);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found.");

        ObjectStorageObjectInfo objectInfo;
        try
        {
            objectInfo = await storageService.StatObjectAsync("servicedesk", req.StorageKey, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            throw new ServiceUnavailableException("Dịch vụ lưu trữ tệp (MinIO) hiện không khả dụng.");
        }

        return await attachmentStore.ConfirmUploadAsync(command.TicketId, actorId, req, objectInfo, clock.UtcNow.UtcDateTime, cancellationToken);
    }
}
