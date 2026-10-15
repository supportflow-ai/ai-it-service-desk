namespace ServiceDesk.Domain.Ticketing;

/// <summary>
/// Time urgency level of a ticket.
/// Used in the deterministic Priority calculation matrix (Impact × Urgency).
/// </summary>
public enum Urgency
{
    Low,
    Medium,
    High
}
