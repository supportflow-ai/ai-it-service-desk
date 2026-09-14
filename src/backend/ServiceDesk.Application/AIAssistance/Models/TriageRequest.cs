namespace ServiceDesk.Application.AIAssistance.Models;

/// <summary>
/// Data-minimized input for AI ticket classification.
/// Only contains fields the AI classifier needs — no secrets, no internal metadata.
///
/// TicketRevision and InputHash are captured at request time for stale protection.
/// </summary>
public sealed record TriageRequest
{
    /// <summary>Ticket being analyzed.</summary>
    public required Guid TicketId { get; init; }

    /// <summary>
    /// Ticket revision at the time of request.
    /// Used to detect if the ticket changed between analysis and review.
    /// </summary>
    public required long TicketRevision { get; init; }

    /// <summary>Ticket title — primary classification signal.</summary>
    public required string Title { get; init; }

    /// <summary>Ticket description — secondary classification signal.</summary>
    public required string Description { get; init; }
}
