using ServiceDesk.Modules.AIAssistance.Application.Contracts;
using ServiceDesk.Modules.AIAssistance.Application.Ports;
using ServiceDesk.Modules.AIAssistance.Domain;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.AIAssistance.Application.Triage;

public interface IGetTicketTriage
{
    Task<AgentTriageView> ExecuteAsync(Guid ticketId, CancellationToken cancellationToken = default);
}

internal sealed class GetTicketTriageUseCase(
    IAiAnalysisStore analysisStore,
    IRequestAiTriage requestAiTriage) : IGetTicketTriage
{
    public async Task<AgentTriageView> ExecuteAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var analysis = await analysisStore.GetLatestAnalysisAsync(ticketId, cancellationToken);
        if (analysis is null)
        {
            return await requestAiTriage.ExecuteAsync(ticketId, cancellationToken);
        }

        if (!analysis.Succeeded)
        {
            return new AgentTriageView
            {
                Outcome = TriageOutcome.Unavailable,
                FailureReason = analysis.FailureReason
            };
        }

        TicketCategory? cat = null;
        Impact? imp = null;
        Urgency? urg = null;
        double? catScore = null;
        double? impScore = null;
        double? urgScore = null;

        foreach (var s in analysis.Suggestions)
        {
            if (s.Type == SuggestionType.Category)
            {
                cat = TriagePolicy.ParseCategory(s.SuggestedValue);
                catScore = s.ModelScore;
            }
            else if (s.Type == SuggestionType.Impact)
            {
                imp = TriagePolicy.ParseImpact(s.SuggestedValue);
                impScore = s.ModelScore;
            }
            else if (s.Type == SuggestionType.Urgency)
            {
                urg = TriagePolicy.ParseUrgency(s.SuggestedValue);
                urgScore = s.ModelScore;
            }
        }

        return new AgentTriageView
        {
            Outcome = TriageOutcome.Success,
            SuggestedCategory = cat,
            SuggestedImpact = imp,
            SuggestedUrgency = urg,
            CategoryScore = catScore,
            ImpactScore = impScore,
            UrgencyScore = urgScore
        };
    }
}
