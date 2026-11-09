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

        var scope = TicketPermissions.ReadScopeFor(currentUser, actorId, includeOwner: true);

        var ticket = await ticketQueries.GetByIdAsync(query.TicketId, scope, cancellationToken);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found.");

        return await attachmentStore.GetForTicketAsync(query.TicketId, cancellationToken);
    }
}
