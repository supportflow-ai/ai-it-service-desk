using ServiceDesk.Modules.AIAssistance.Domain;

namespace ServiceDesk.Modules.AIAssistance.Application.Ports;

public interface IAiAnalysisStore
{
    Task SaveAnalysisAsync(AIAnalysis analysis, CancellationToken cancellationToken = default);
    Task<AIAnalysis?> GetLatestAnalysisAsync(Guid ticketId, CancellationToken cancellationToken = default);
}
