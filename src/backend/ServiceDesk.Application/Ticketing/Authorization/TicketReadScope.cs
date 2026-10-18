using ServiceDesk.Application.Common;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Ticketing.Authorization;

public abstract record TicketReadScope
{
    private TicketReadScope() { }
    public sealed record OwnTickets : TicketReadScope
    {
        public Guid ActorId { get; }
        public OwnTickets(Guid actorId)
        {
            if (actorId == Guid.Empty) throw new ArgumentException("Owner identity is required.", nameof(actorId));
            ActorId = actorId;
        }
    }
    public sealed record AllTickets : TicketReadScope;
}

internal static class TicketActor
{
    public static Guid RequireId(ICurrentUser currentUser)
    {
        var id = AuthenticatedActor.RequireId(currentUser);
        if (!currentUser.Roles.Any(RoleNames.All.Contains))
            throw new UnauthorizedAccessException("Requester role is required.");
        return id;
    }
}
