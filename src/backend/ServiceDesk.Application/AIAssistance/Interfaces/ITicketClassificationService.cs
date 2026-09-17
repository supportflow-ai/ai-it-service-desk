using ServiceDesk.Application.AIAssistance.Models;

namespace ServiceDesk.Application.AIAssistance.Interfaces;

/// <summary>
/// AI-powered ticket classification (category, impact, urgency suggestion).
/// Infrastructure provides NoOp implementation for bootstrap; real provider later.
///
/// CANONICAL CONTRACT: Use ClassifyAsync(TriageRequest, CancellationToken) for all new code.
/// The legacy ClassifyAsync(string, string, CancellationToken) overload is retained for
/// backward compatibility and will be removed once all consumers migrate.
/// </summary>
public interface ITicketClassificationService
{
    /// <summary>
    /// Classify a ticket based on a data-minimized triage request.
    /// Returns a typed <see cref="TriageResult"/> with explicit outcome semantics.
    ///
    /// Implementations MUST:
    /// - Not throw on provider failures (return Unavailable/Timeout/InvalidResponse).
    /// - Propagate the CancellationToken to underlying calls.
    /// - Validate output before returning (unknown category → InvalidResponse).
    /// - Not return fake confidence scores.
    /// </summary>
    Task<TriageResult> ClassifyAsync(
        TriageRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of AI ticket classification.
///
/// LEGACY: This type is retained for backward compatibility with existing tests.
/// New code should use <see cref="TriageResult"/> instead.
/// Relationship: TriageResult is the canonical replacement with typed enums,
/// per-field scores, and explicit outcome semantics.
/// </summary>
public sealed record TicketClassificationResult
{
    public string? SuggestedCategory { get; init; }
    public string? SuggestedImpact { get; init; }
    public string? SuggestedUrgency { get; init; }
    public double Confidence { get; init; }

    public static TicketClassificationResult Empty => new()
    {
        Confidence = 0
    };
}
