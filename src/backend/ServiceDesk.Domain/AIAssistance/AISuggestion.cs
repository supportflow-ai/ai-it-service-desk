namespace ServiceDesk.Domain.AIAssistance;

/// <summary>
/// An individual AI suggestion within an <see cref="AIAnalysis"/>.
/// Represents a proposed value for a specific ticket field (Category, Impact, Urgency).
///
/// Decision lifecycle:
/// - Created as Pending.
/// - Human reviewer moves to Accepted/Overridden/Rejected (terminal).
/// - System moves to Expired if the ticket changes (stale).
/// - Terminal states cannot be changed.
/// </summary>
public class AISuggestion
{
    public Guid Id { get; private set; }

    /// <summary>FK to the parent analysis.</summary>
    public Guid AnalysisId { get; private set; }

    /// <summary>Which field this suggestion applies to.</summary>
    public SuggestionType Type { get; private set; }

    /// <summary>The AI-proposed value (e.g., "NET", "High").</summary>
    public string SuggestedValue { get; private set; } = string.Empty;

    /// <summary>
    /// Raw model score for this suggestion, range [0.0, 1.0].
    /// Null if the provider does not return per-field scores.
    /// NOT a calibrated probability — see <see cref="ConfidencePolicy"/>.
    /// </summary>
    public double? ModelScore { get; private set; }

    /// <summary>Current decision state.</summary>
    public SuggestionDecision Decision { get; private set; } = SuggestionDecision.Pending;

    /// <summary>
    /// The final applied value.
    /// - Accepted: equals SuggestedValue.
    /// - Overridden: the human-provided replacement.
    /// - Rejected/Expired: null.
    /// </summary>
    public string? FinalValue { get; private set; }

    /// <summary>User ID of the reviewer. Null while Pending.</summary>
    public string? ReviewedBy { get; private set; }

    /// <summary>When the decision was made. Null while Pending.</summary>
    public DateTimeOffset? ReviewedAt { get; private set; }

    private AISuggestion() { } // EF Core

    /// <summary>
    /// Create a new pending suggestion.
    /// </summary>
    public static AISuggestion Create(
        Guid analysisId,
        SuggestionType type,
        string suggestedValue,
        double? modelScore)
    {
        if (string.IsNullOrWhiteSpace(suggestedValue))
            throw new ArgumentException("SuggestedValue is required.", nameof(suggestedValue));

        if (modelScore.HasValue && !ConfidencePolicy.IsValidScore(modelScore.Value))
            throw new ArgumentException(
                $"Model score {modelScore} is invalid. Must be in [{ConfidencePolicy.MinScore}, {ConfidencePolicy.MaxScore}] and not NaN/Infinity.",
                nameof(modelScore));

        return new AISuggestion
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            Type = type,
            SuggestedValue = suggestedValue,
            ModelScore = modelScore
        };
    }

    /// <summary>
    /// Accept the AI suggestion — FinalValue becomes the SuggestedValue.
    /// </summary>
    public void Accept(string reviewedBy, DateTimeOffset reviewedAt)
    {
        EnsurePending();
        ValidateReviewer(reviewedBy);

        Decision = SuggestionDecision.Accepted;
        FinalValue = SuggestedValue;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
    }

    /// <summary>
    /// Override the AI suggestion with a human-provided value.
    /// </summary>
    public void Override(string finalValue, string reviewedBy, DateTimeOffset reviewedAt)
    {
        EnsurePending();
        ValidateReviewer(reviewedBy);

        if (string.IsNullOrWhiteSpace(finalValue))
            throw new ArgumentException("FinalValue is required for Override.", nameof(finalValue));

        Decision = SuggestionDecision.Overridden;
        FinalValue = finalValue;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
    }

    /// <summary>
    /// Reject the AI suggestion — no value is applied.
    /// </summary>
    public void Reject(string reviewedBy, DateTimeOffset reviewedAt)
    {
        EnsurePending();
        ValidateReviewer(reviewedBy);

        Decision = SuggestionDecision.Rejected;
        FinalValue = null;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
    }

    /// <summary>
    /// Mark the suggestion as expired (stale) because the ticket changed.
    /// This is a system action, not a human decision.
    /// </summary>
    public void MarkExpired()
    {
        if (Decision != SuggestionDecision.Pending)
            return; // Only pending suggestions can expire; terminal states are final.

        Decision = SuggestionDecision.Expired;
        FinalValue = null;
    }

    /// <summary>
    /// Whether this suggestion is in a terminal state (no further transitions).
    /// </summary>
    public bool IsTerminal => Decision != SuggestionDecision.Pending;

    private void EnsurePending()
    {
        if (Decision != SuggestionDecision.Pending)
            throw new InvalidOperationException(
                $"Cannot change decision on a suggestion that is already {Decision}.");
    }

    private static void ValidateReviewer(string reviewedBy)
    {
        if (string.IsNullOrWhiteSpace(reviewedBy))
            throw new ArgumentException("ReviewedBy is required.", nameof(reviewedBy));
    }
}
