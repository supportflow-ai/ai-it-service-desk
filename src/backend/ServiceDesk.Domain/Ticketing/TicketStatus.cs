namespace ServiceDesk.Domain.Ticketing;

public enum TicketStatus
{
    Draft = 0,
    Submitted = 1,
    InProgress = 2,
    PendingUser = 3,
    Resolved = 4,
    Closed = 5,
    Cancelled = 6
}
