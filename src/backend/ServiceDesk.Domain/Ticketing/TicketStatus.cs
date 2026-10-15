namespace ServiceDesk.Domain.Ticketing;

public enum TicketStatus
{
    Draft = 0,
    Submitted = 1,
    InProgress = 2,
    PendingUser = 3,
    Resolved = 4,
    Closed = 5,
    Cancelled = 6,
    // Keep persisted values stable when adding the extended workflow states.
    Triaged = 7,
    Assigned = 8,
    PendingExternal = 9
}
