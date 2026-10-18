using ServiceDesk.Application.AIAssistance.Contracts;

namespace ServiceDesk.Application.AIAssistance.Ports;

/// <summary>
/// AI-powered ticket classification (category, impact, urgency suggestion).
/// Infrastructure provides NoOp implementation for bootstrap; real provider later.
///
/// Use the data-minimized ticket snapshot for classification.
/// </summary>
public interface ITicketClassificationService
{
    /// <summary>
    /// Classify a ticket based on a data-minimized triage request.
    /// Returns a typed <see cref="TriageResult"/> with explicit outcome semantics.
    ///
    /// Implementations MUST:
    /// - Not throw on provider failures (return Unavailable/Timeout/InvalidResponse).
    /// - Observe pre-cancelled tokens and propagate cancellation; never convert it to Unavailable.
    /// - Validate output before returning (unknown category â†’ InvalidResponse).
    /// - Not return fake confidence scores.
    /// </summary>
    Task<TriageResult> ClassifyAsync(
        TriageRequest request,
        CancellationToken cancellationToken = default);
}
