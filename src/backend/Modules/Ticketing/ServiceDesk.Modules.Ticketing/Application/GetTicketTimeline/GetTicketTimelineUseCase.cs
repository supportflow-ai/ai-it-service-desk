using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Ticketing.Application.GetTicketTimeline;

public record GetTicketTimelineQuery(Guid TicketId);

public interface IGetTicketTimeline
{
    Task<IReadOnlyList<TimelineItemDto>> ExecuteAsync(GetTicketTimelineQuery query, CancellationToken cancellationToken = default);
}

internal sealed class GetTicketTimelineUseCase(
    ITicketQueries ticketQueries,
    ITicketTimelineStore timelineStore,
    ICurrentUser currentUser) : IGetTicketTimeline
{
    public async Task<IReadOnlyList<TimelineItemDto>> ExecuteAsync(GetTicketTimelineQuery query, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(query);

        var scope = TicketPermissions.ReadScopeFor(currentUser, actorId);
        var ticket = await ticketQueries.GetByIdAsync(query.TicketId, scope, cancellationToken);
        var includeInternal = scope is TicketReadScope.AgentTickets or TicketReadScope.AllTickets;
        if (ticket is null && scope is TicketReadScope.AgentTickets)
        {
            // Quyền chủ sở hữu chỉ cấp phần công khai khi người gọi không có quyền nhân viên trên phiếu.
            ticket = await ticketQueries.GetByIdAsync(query.TicketId, new TicketReadScope.OwnTickets(actorId), cancellationToken);
            includeInternal = false;
        }

        if (ticket == null)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        return await timelineStore.GetForTicketAsync(query.TicketId, includeInternal, cancellationToken);
    }
}
