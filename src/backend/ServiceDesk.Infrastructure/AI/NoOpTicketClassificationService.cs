using ServiceDesk.Application.AIAssistance.Interfaces;
using ServiceDesk.Application.AIAssistance.Models;

namespace ServiceDesk.Infrastructure.AI;

/// <summary>
/// No-op ticket classification — returns controlled Unavailable result.
/// Placeholder until a real AI provider is integrated.
///
/// Contract compliance:
/// - Does not throw.
/// - Returns a well-formed TriageResult with Unavailable outcome.
/// - Does not return fake confidence scores.
/// - Propagates (but does not use) CancellationToken.
/// </summary>
public sealed class NoOpTicketClassificationService : ITicketClassificationService
{
    public Task<TriageResult> ClassifyAsync(
        TriageRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            TriageResult.Unavailable("No AI provider configured. Using NoOp fallback."));
    }
}
