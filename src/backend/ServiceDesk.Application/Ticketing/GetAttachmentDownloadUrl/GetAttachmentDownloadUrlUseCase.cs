using ServiceDesk.Application.Common;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Ticketing.GetAttachmentDownloadUrl;

public record GetAttachmentDownloadUrlQuery(Guid TicketId, string StorageKey);

public interface IGetAttachmentDownloadUrl
{
    Task<DownloadUrlResult> ExecuteAsync(GetAttachmentDownloadUrlQuery query, CancellationToken cancellationToken = default);
}

internal sealed class GetAttachmentDownloadUrlUseCase(
    ITicketQueries ticketQueries,
    IObjectStorageService storageService,
    ICurrentUser currentUser) : IGetAttachmentDownloadUrl
{
    public async Task<DownloadUrlResult> ExecuteAsync(GetAttachmentDownloadUrlQuery query, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.StorageKey))
            throw new ArgumentException("Storage key is required.");

        var expectedPrefix = $"tickets/{query.TicketId}/";
        if (!query.StorageKey.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Tệp đính kèm không thuộc về yêu cầu này.");

        var isStaff = currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin);
        var scope = isStaff ? (TicketReadScope)new TicketReadScope.AllTickets() : new TicketReadScope.OwnTickets(actorId);

        var ticket = await ticketQueries.GetByIdAsync(query.TicketId, scope, cancellationToken);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found.");

        const int expirySeconds = 3600;
        try
        {
            var downloadUrl = await storageService.GetPresignedDownloadUrlAsync("servicedesk", query.StorageKey, expirySeconds, cancellationToken);
            return new DownloadUrlResult(downloadUrl);
        }
        catch (Exception ex)
        {
            throw new ServiceUnavailableException($"Dịch vụ lưu trữ tệp (MinIO) hiện không khả dụng: {ex.Message}");
        }
    }
}
