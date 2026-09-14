using ServiceDesk.Domain.AIAssistance;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.AIAssistance.Models;

/// <summary>
/// Result of an AI triage classification attempt.
/// Represents the complete outcome including success/failure state.
///
/// DESIGN DECISIONS:
/// - SuggestedCategory/Impact/Urgency use domain enums (typed, not free-text).
/// - ModelScore is raw [0.0, 1.0], NOT a calibrated probability.
/// - ConfidenceBand is a product label derived from ModelScore via ConfidencePolicy.
/// - Priority is NEVER included — it is computed by deterministic PriorityMatrix.
/// - Outcome indicates success/failure/degraded state.
/// </summary>
public sealed record TriageResult
{
    /// <summary>
    /// Overall outcome of the classification attempt.
    /// </summary>
    public required TriageOutcome Outcome { get; init; }

    /// <summary>AI-suggested category, if classification succeeded.</summary>
    public TicketCategory? SuggestedCategory { get; init; }

    /// <summary>AI-suggested impact level, if classification succeeded.</summary>
    public Impact? SuggestedImpact { get; init; }

    /// <summary>AI-suggested urgency level, if classification succeeded.</summary>
    public Urgency? SuggestedUrgency { get; init; }

    /// <summary>
    /// Raw model score for category suggestion, range [0.0, 1.0].
    /// Null if unavailable or classification failed.
    /// NOT a calibrated probability — see <see cref="ConfidencePolicy"/>.
    /// </summary>
    public double? CategoryScore { get; init; }

    /// <summary>Raw model score for impact suggestion.</summary>
    public double? ImpactScore { get; init; }

    /// <summary>Raw model score for urgency suggestion.</summary>
    public double? UrgencyScore { get; init; }

    /// <summary>
    /// Product-facing confidence band for the category suggestion.
    /// Derived from CategoryScore via <see cref="ConfidencePolicy.Classify"/>.
    /// </summary>
    public ConfidenceBand? CategoryConfidence => ConfidencePolicy.Classify(CategoryScore);

    /// <summary>Product-facing confidence band for impact suggestion.</summary>
    public ConfidenceBand? ImpactConfidence => ConfidencePolicy.Classify(ImpactScore);

    /// <summary>Product-facing confidence band for urgency suggestion.</summary>
    public ConfidenceBand? UrgencyConfidence => ConfidencePolicy.Classify(UrgencyScore);

    /// <summary>Whether the suggested category requires mandatory manual triage.</summary>
    public bool RequiresManualTriage =>
        SuggestedCategory.HasValue && TriagePolicy.RequiresManualTriage(SuggestedCategory.Value);

    /// <summary>
    /// Controlled failure information when Outcome is not Success.
    /// Must not leak raw provider exceptions.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Validation errors from output validation (unknown category, invalid score, etc.).
    /// </summary>
    public IReadOnlyList<string> ValidationErrors { get; init; } = [];

    /// <summary>
    /// Factory for a successful classification result.
    /// </summary>
    public static TriageResult Success(
        TicketCategory? category,
        Impact? impact,
        Urgency? urgency,
        double? categoryScore = null,
        double? impactScore = null,
        double? urgencyScore = null)
    {
        return new TriageResult
        {
            Outcome = TriageOutcome.Success,
            SuggestedCategory = category,
            SuggestedImpact = impact,
            SuggestedUrgency = urgency,
            CategoryScore = categoryScore,
            ImpactScore = impactScore,
            UrgencyScore = urgencyScore
        };
    }

    /// <summary>
    /// Factory for when the AI provider is unavailable.
    /// Core workflow must remain usable.
    /// </summary>
    public static TriageResult Unavailable(string reason = "AI classification service is unavailable.")
    {
        return new TriageResult
        {
            Outcome = TriageOutcome.Unavailable,
            FailureReason = reason
        };
    }

    /// <summary>
    /// Factory for when the AI provider times out.
    /// </summary>
    public static TriageResult Timeout(string reason = "AI classification timed out.")
    {
        return new TriageResult
        {
            Outcome = TriageOutcome.Timeout,
            FailureReason = reason
        };
    }

    /// <summary>
    /// Factory for when the AI provider returns an invalid/unparseable response.
    /// </summary>
    public static TriageResult InvalidResponse(string reason, IReadOnlyList<string>? validationErrors = null)
    {
        return new TriageResult
        {
            Outcome = TriageOutcome.InvalidResponse,
            FailureReason = reason,
            ValidationErrors = validationErrors ?? []
        };
    }
}

/// <summary>
/// Outcome of a triage classification attempt.
/// </summary>
public enum TriageOutcome
{
    /// <summary>Classification completed successfully.</summary>
    Success,

    /// <summary>AI provider is not available — core workflow continues without AI.</summary>
    Unavailable,

    /// <summary>AI provider timed out — core workflow continues without AI.</summary>
    Timeout,

    /// <summary>AI returned invalid/unparseable output — treated as degraded.</summary>
    InvalidResponse,

    /// <summary>Output validation failed (unknown category, invalid score, etc.).</summary>
    ValidationFailed
}
