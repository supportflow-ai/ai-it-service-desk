using ServiceDesk.Domain.AIAssistance;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.AIAssistance.Models;

/// <summary>
/// Requester-safe projection of a triage result.
/// Requester can see:
/// - Category suggestion + confidence band
/// - Published KB recommendations (when available)
///
/// Requester CANNOT see:
/// - Impact/Urgency suggestions (Agent/Lead scope)
/// - Historical tickets or resolutions
/// - Internal notes
/// - Similar resolved tickets
/// - Raw model scores
/// </summary>
public sealed record RequesterTriageView
{
    public required TriageOutcome Outcome { get; init; }
    public TicketCategory? SuggestedCategory { get; init; }
    public ConfidenceBand? CategoryConfidence { get; init; }
    public bool RequiresManualTriage { get; init; }
    public string? FailureReason { get; init; }
    // Future: public IReadOnlyList<PublishedKBReference> KBRecommendations { get; init; }

    public static RequesterTriageView FromTriageResult(TriageResult result)
    {
        return new RequesterTriageView
        {
            Outcome = result.Outcome,
            SuggestedCategory = result.SuggestedCategory,
            CategoryConfidence = result.CategoryConfidence,
            RequiresManualTriage = result.RequiresManualTriage,
            FailureReason = result.FailureReason
        };
    }
}

/// <summary>
/// Agent/Lead/Admin projection of a triage result.
/// Full visibility into all suggestions, scores, and evidence.
/// Requires authorization check before serving.
/// </summary>
public sealed record AgentTriageView
{
    public required TriageOutcome Outcome { get; init; }
    public TicketCategory? SuggestedCategory { get; init; }
    public Impact? SuggestedImpact { get; init; }
    public Urgency? SuggestedUrgency { get; init; }
    public double? CategoryScore { get; init; }
    public double? ImpactScore { get; init; }
    public double? UrgencyScore { get; init; }
    public ConfidenceBand? CategoryConfidence { get; init; }
    public ConfidenceBand? ImpactConfidence { get; init; }
    public ConfidenceBand? UrgencyConfidence { get; init; }
    public bool RequiresManualTriage { get; init; }
    public string? FailureReason { get; init; }
    public IReadOnlyList<string> ValidationErrors { get; init; } = [];

    public static AgentTriageView FromTriageResult(TriageResult result)
    {
        return new AgentTriageView
        {
            Outcome = result.Outcome,
            SuggestedCategory = result.SuggestedCategory,
            SuggestedImpact = result.SuggestedImpact,
            SuggestedUrgency = result.SuggestedUrgency,
            CategoryScore = result.CategoryScore,
            ImpactScore = result.ImpactScore,
            UrgencyScore = result.UrgencyScore,
            CategoryConfidence = result.CategoryConfidence,
            ImpactConfidence = result.ImpactConfidence,
            UrgencyConfidence = result.UrgencyConfidence,
            RequiresManualTriage = result.RequiresManualTriage,
            FailureReason = result.FailureReason,
            ValidationErrors = result.ValidationErrors
        };
    }
}
