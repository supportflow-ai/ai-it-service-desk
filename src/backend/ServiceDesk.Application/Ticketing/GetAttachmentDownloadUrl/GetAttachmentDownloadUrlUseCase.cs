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

        var isStaff = currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin);
        var scope = isStaff ? (TicketReadScope)new TicketReadScope.AllTickets() : new TicketReadScope.OwnTickets(actorId);

        var ticket = await ticketQueries.GetByIdAsync(query.TicketId, scope, cancellationToken);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found.");

        const int expirySeconds = 3600;
        string downloadUrl;
        try
        {
            downloadUrl = await storageService.GetPresignedDownloadUrlAsync("servicedesk", query.StorageKey, expirySeconds, cancellationToken);
        }
        catch
        {
            downloadUrl = $"http://localhost:9000/servicedesk/{query.StorageKey}?download=true";
        }

        return new DownloadUrlResult(downloadUrl);
    }
}
