using ServiceDesk.Modules.AIAssistance.Application.Contracts;
using ServiceDesk.Modules.AIAssistance.Application.Ports;
using ServiceDesk.Modules.AIAssistance.Domain;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.AIAssistance.Application.Triage;

public interface IGetTicketTriage
{
    Task<AgentTriageView> ExecuteAsync(Guid ticketId, CancellationToken cancellationToken = default);
}

internal sealed class GetTicketTriageUseCase(
    IAiAnalysisStore analysisStore,
    IRequestAiTriage requestAiTriage,
    ITicketTriageBridge ticketBridge,
    ICurrentUser currentUser) : IGetTicketTriage
{
    public async Task<AgentTriageView> ExecuteAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var actorId = AuthenticatedActor.RequireId(currentUser);
        var snapshot = await ticketBridge.GetSnapshotAsync(ticketId, cancellationToken);
        if (snapshot is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy vé hỗ trợ có mã {ticketId}.");
        }

        var isStaff = currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin);
        if (!isStaff && snapshot.RequesterId != actorId)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền xem thông tin phân loại của vé hỗ trợ này.");
        }

        var analysis = await analysisStore.GetLatestAnalysisAsync(ticketId, cancellationToken);
        if (analysis is null)
        {
            if (isStaff)
            {
                return await requestAiTriage.ExecuteAsync(ticketId, cancellationToken);
            }

            return new AgentTriageView
            {
                Outcome = TriageOutcome.Unavailable,
                FailureReason = "Chưa có kết quả phân loại AI cho vé hỗ trợ này."
            };
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
