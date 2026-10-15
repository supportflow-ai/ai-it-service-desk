namespace ServiceDesk.Domain.Ticketing;

/// <summary>
/// Canonical ticket category codes.
/// Used by AI triage to validate suggested categories against the official taxonomy.
/// Full category management (CRUD, active/inactive) is a Ticketing-team concern;
/// this enum establishes the stable contract for AI classification validation.
/// </summary>
public enum TicketCategory
{
    /// <summary>Account issues (password, login, profile).</summary>
    ACC,

    /// <summary>Network connectivity.</summary>
    NET,

    /// <summary>VPN / remote access.</summary>
    VPN,

    /// <summary>Email and messaging.</summary>
    MAIL,

    /// <summary>Software installation, licensing, updates.</summary>
    SW,

    /// <summary>Hardware failures, replacements.</summary>
    HW,

    /// <summary>Printing and scanning.</summary>
    PRINT,

    /// <summary>Access control, permissions, group membership.</summary>
    ACCESS,

    /// <summary>Equipment requests (laptop, monitor, peripherals).</summary>
    EQUIP,

    /// <summary>Security incident — always requires manual triage.</summary>
    SEC,

    /// <summary>Uncategorized / other.</summary>
    OTHER
}
