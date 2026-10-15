namespace ServiceDesk.Domain.Ticketing;

/// <summary>
/// Computed ticket priority (P1–P4).
/// Priority is ALWAYS computed by deterministic business rules (Impact × Urgency).
/// AI MUST NOT set or suggest Priority directly — it may only suggest Impact and Urgency.
/// </summary>
public enum Priority
{
    /// <summary>Critical: requires immediate response.</summary>
    P1,

    /// <summary>High: significant impact, needs prompt attention.</summary>
    P2,

    /// <summary>Medium: moderate impact, standard response.</summary>
    P3,

    /// <summary>Low: minimal impact, scheduled handling.</summary>
    P4
}
