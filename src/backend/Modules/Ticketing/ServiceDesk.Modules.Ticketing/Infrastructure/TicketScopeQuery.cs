using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Infrastructure;

internal static class TicketScopeQuery
{
    public static IQueryable<Ticket> Apply(IQueryable<Ticket> query, DbContext context, TicketReadScope scope)
    {
        if (scope is TicketReadScope.AllTickets) return query;
        if (scope is TicketReadScope.OwnTickets own) return query.Where(ticket => ticket.RequesterId == own.ActorId);
        if (scope is not TicketReadScope.AgentTickets agent)
            throw new ArgumentException("Yêu cầu phạm vi quyền đọc hợp lệ.", nameof(scope));

        var actorId = agent.ActorId;
        var includeOwn = agent.IncludeOwnTickets;
        var memberships = PostgresSupportTeamMembershipReader.ActiveMembers(context)
            .Where(member => member.UserId == actorId);
        return query.Where(ticket => ticket.AssignedToId == actorId ||
            (includeOwn && ticket.RequesterId == actorId) ||
            memberships.Any(member => member.TeamId == ticket.SupportTeamId));
    }
}
