namespace ServiceDesk.Domain.Ticketing;

/// <summary>
/// Ticket workflow states.
/// Provided as a minimal primitive for AI contract to reference — AI analysis
/// is a side-assistance that MUST NOT transition ticket status.
/// Full state-machine enforcement is a Ticketing-team concern.
/// </summary>
public enum TicketStatus
{
    Draft,
    Submitted,
    Triaged,
    Assigned,
    InProgress,
    PendingUser,
    PendingExternal,
    Resolved,
    Closed
}
