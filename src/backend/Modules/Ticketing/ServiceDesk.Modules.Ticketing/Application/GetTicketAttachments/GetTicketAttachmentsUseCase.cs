using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Ticketing.Application.GetTicketAttachments;

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

        var isStaff = TicketPermissions.IsStaff(currentUser);
        var scope = isStaff ? (TicketReadScope)new TicketReadScope.AllTickets() : new TicketReadScope.OwnTickets(actorId);

        var ticket = await ticketQueries.GetByIdAsync(query.TicketId, scope, cancellationToken);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found.");

        return await attachmentStore.GetForTicketAsync(query.TicketId, cancellationToken);
    }
}
