using System.Collections.Concurrent;
using ServiceDesk.Modules.AIAssistance.Application.Ports;
using ServiceDesk.Modules.AIAssistance.Domain;

namespace ServiceDesk.Modules.AIAssistance.Infrastructure;

internal sealed class InMemoryAiAnalysisStore : IAiAnalysisStore
{
    private readonly ConcurrentDictionary<Guid, AIAnalysis> _analyses = new();

    public Task SaveAnalysisAsync(AIAnalysis analysis, CancellationToken cancellationToken = default)
    {
        _analyses[analysis.TicketId] = analysis;
        return Task.CompletedTask;
    }

    public Task<AIAnalysis?> GetLatestAnalysisAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        _analyses.TryGetValue(ticketId, out var analysis);
        return Task.FromResult(analysis);
    }
}
