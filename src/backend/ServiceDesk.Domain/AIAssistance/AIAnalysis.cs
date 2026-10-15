namespace ServiceDesk.Domain.AIAssistance;

/// <summary>
/// Represents a single AI analysis run against a ticket snapshot.
/// Stores the ticket revision and input hash at the time of analysis
/// for stale-suggestion protection.
///
/// An AIAnalysis contains zero or more <see cref="AISuggestion"/> items.
///
/// Integration note: TicketId references the Ticket entity that will be
/// provided by the Ticketing team. The FK relationship will be configured
/// when the Ticket entity exists.
/// </summary>
public class AIAnalysis
{
    public Guid Id { get; private set; }

    /// <summary>
    /// Reference to the ticket being analyzed.
    /// </summary>
    public Guid TicketId { get; private set; }

    /// <summary>
    /// Snapshot of the ticket's revision number at the time of analysis.
    /// Used for stale detection: if the current ticket revision differs,
    /// all suggestions from this analysis are stale.
    /// </summary>
    public long TicketRevision { get; private set; }

    /// <summary>
    /// Deterministic hash of the AI-relevant input fields (Title + Description).
    /// Used as a secondary stale check: if the ticket content changed but
    /// revision was not incremented, the hash catches the discrepancy.
    /// </summary>
    public string InputHash { get; private set; } = string.Empty;

    /// <summary>
    /// When this analysis was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Whether the AI provider returned a successful classification.
    /// False indicates the result came from a fallback/degraded path.
    /// </summary>
    public bool Succeeded { get; private set; }

    /// <summary>
    /// Optional failure reason when Succeeded is false.
    /// Must not leak raw provider exceptions — use controlled failure codes.
    /// </summary>
    public string? FailureReason { get; private set; }

    /// <summary>
    /// The individual field suggestions produced by this analysis.
    /// </summary>
    public IReadOnlyList<AISuggestion> Suggestions => _suggestions.AsReadOnly();
    private readonly List<AISuggestion> _suggestions = [];

    private AIAnalysis() { } // EF Core

    /// <summary>
    /// Create a new successful AI analysis.
    /// </summary>
    public static AIAnalysis CreateSucceeded(
        Guid ticketId,
        long ticketRevision,
        string inputHash,
        DateTimeOffset createdAt)
    {
        return new AIAnalysis
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            TicketRevision = ticketRevision,
            InputHash = inputHash ?? throw new ArgumentNullException(nameof(inputHash)),
            CreatedAt = createdAt,
            Succeeded = true
        };
    }

    /// <summary>
    /// Create a failed/degraded analysis record.
    /// </summary>
    public static AIAnalysis CreateFailed(
        Guid ticketId,
        long ticketRevision,
        string inputHash,
        DateTimeOffset createdAt,
        string failureReason)
    {
        return new AIAnalysis
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            TicketRevision = ticketRevision,
            InputHash = inputHash ?? throw new ArgumentNullException(nameof(inputHash)),
            CreatedAt = createdAt,
            Succeeded = false,
            FailureReason = failureReason ?? throw new ArgumentNullException(nameof(failureReason))
        };
    }

    /// <summary>
    /// Add a suggestion to this analysis.
    /// Only allowed on successful analyses.
    /// </summary>
    public void AddSuggestion(AISuggestion suggestion)
    {
        if (!Succeeded)
            throw new InvalidOperationException("Cannot add suggestions to a failed analysis.");

        ArgumentNullException.ThrowIfNull(suggestion);
        _suggestions.Add(suggestion);
    }
}
