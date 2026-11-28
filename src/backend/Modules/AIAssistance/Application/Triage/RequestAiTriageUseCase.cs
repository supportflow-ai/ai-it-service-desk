using ServiceDesk.Modules.AIAssistance.Application.Contracts;
using ServiceDesk.Modules.AIAssistance.Application.Ports;
using ServiceDesk.Modules.AIAssistance.Domain;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.AIAssistance.Application.Triage;

public interface IRequestAiTriage
{
    Task<AgentTriageView> ExecuteAsync(Guid ticketId, CancellationToken cancellationToken = default);
}

internal sealed class RequestAiTriageUseCase(
    ITicketTriageBridge ticketBridge,
    ITicketClassificationService classificationService,
    IAiAnalysisStore analysisStore,
    IClock clock) : IRequestAiTriage
{
    public async Task<AgentTriageView> ExecuteAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var snapshot = await ticketBridge.GetSnapshotAsync(ticketId, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy vé hỗ trợ có mã {ticketId}.");
        if (!snapshot.CanTriage)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền phân tích AI cho vé hỗ trợ này.");
        }

        var inputHash = InputHashComputer.Compute(snapshot.Title, snapshot.Description);
        var now = clock.UtcNow;

        var request = new TriageRequest
        {
            TicketId = ticketId,
            TicketRevision = snapshot.Revision,
            Title = snapshot.Title,
            Description = snapshot.Description
        };

        var triageResult = await classificationService.ClassifyAsync(request, cancellationToken);

        AIAnalysis analysis;
        if (triageResult.Outcome == TriageOutcome.Success)
        {
            analysis = AIAnalysis.CreateSucceeded(ticketId, snapshot.Revision, inputHash, now);
            if (triageResult.SuggestedCategory.HasValue)
            {
                analysis.AddSuggestion(AISuggestion.Create(
                    analysis.Id,
                    SuggestionType.Category,
                    triageResult.SuggestedCategory.Value.ToString(),
                    triageResult.CategoryScore));
            }
            if (triageResult.SuggestedImpact.HasValue)
            {
                analysis.AddSuggestion(AISuggestion.Create(
                    analysis.Id,
                    SuggestionType.Impact,
                    triageResult.SuggestedImpact.Value.ToString(),
                    triageResult.ImpactScore));
            }
            if (triageResult.SuggestedUrgency.HasValue)
            {
                analysis.AddSuggestion(AISuggestion.Create(
                    analysis.Id,
                    SuggestionType.Urgency,
                    triageResult.SuggestedUrgency.Value.ToString(),
                    triageResult.UrgencyScore));
            }
        }
        else
        {
            analysis = AIAnalysis.CreateFailed(
                ticketId,
                snapshot.Revision,
                inputHash,
                now,
                triageResult.FailureReason ?? "Phân loại AI thất bại hoặc dịch vụ AI chưa kích hoạt.",
                triageResult.Outcome, triageResult.ValidationErrors);
        }

        await analysisStore.AddAsync(analysis, cancellationToken);
        return AgentTriageView.FromTriageResult(triageResult).WithAnalysis(analysis, isStale: false);
    }
}
