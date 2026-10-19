namespace ServiceDesk.Domain.Ticketing;

/// <summary>
/// Ticket workflow states.
/// AI analysis is side-assistance and must not transition ticket status.
/// The Ticket aggregate enforces the MVP workflow.
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
    Closed,
    Cancelled
}
