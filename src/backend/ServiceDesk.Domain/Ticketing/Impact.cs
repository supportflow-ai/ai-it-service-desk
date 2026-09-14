namespace ServiceDesk.Domain.Ticketing;

/// <summary>
/// Business impact level of a ticket.
/// Used in the deterministic Priority calculation matrix (Impact × Urgency).
/// </summary>
public enum Impact
{
    Low,
    Medium,
    High
}
