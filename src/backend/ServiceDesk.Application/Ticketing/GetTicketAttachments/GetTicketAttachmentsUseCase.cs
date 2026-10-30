using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Ticketing.GetTicketAttachments;

public record GetTicketAttachmentsQuery(Guid TicketId);

public interface IGetTicketAttachments
{
    Task<IReadOnlyList<TicketAttachmentDto>> ExecuteAsync(GetTicketAttachmentsQuery query, CancellationToken cancellationToken = default);
}

internal sealed class GetTicketAttachmentsUseCase(
    ITicketQueries ticketQueries,
    ITicketAttachmentStore attachmentStore,
    ICurrentUser currentUser) : IGetTicketAttachments
{
    public async Task<IReadOnlyList<TicketAttachmentDto>> ExecuteAsync(GetTicketAttachmentsQuery query, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(query);

        var isStaff = currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin);
        var scope = isStaff ? (TicketReadScope)new TicketReadScope.AllTickets() : new TicketReadScope.OwnTickets(actorId);

        var ticket = await ticketQueries.GetByIdAsync(query.TicketId, scope, cancellationToken);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found.");

        return await attachmentStore.GetForTicketAsync(query.TicketId, cancellationToken);
    }
}
