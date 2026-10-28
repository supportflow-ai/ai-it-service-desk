using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Ticketing.GetTicketTimeline;

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

        var isStaff = currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin);
        var scope = isStaff ? (TicketReadScope)new TicketReadScope.AllTickets() : new TicketReadScope.OwnTickets(actorId);

        var ticket = await ticketQueries.GetByIdAsync(query.TicketId, scope, cancellationToken);
        if (ticket == null)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        // QUY TẮC AN TOÀN SỐ 1: Phía server lọc bỏ hoàn toàn ghi chú nội bộ nếu caller không phải Staff
        var includeInternal = isStaff;

        return await timelineStore.GetForTicketAsync(query.TicketId, includeInternal, cancellationToken);
    }
}
