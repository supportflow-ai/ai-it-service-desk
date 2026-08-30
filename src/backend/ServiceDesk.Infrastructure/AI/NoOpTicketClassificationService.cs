using ServiceDesk.Application.AIAssistance.Interfaces;

namespace ServiceDesk.Infrastructure.AI;

/// <summary>
/// No-op ticket classification — returns empty result.
/// Placeholder until a real AI provider is integrated.
/// </summary>
public sealed class NoOpTicketClassificationService : ITicketClassificationService
{
    public Task<TicketClassificationResult> ClassifyAsync(
        string title, string description, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(TicketClassificationResult.Empty);
    }
}
