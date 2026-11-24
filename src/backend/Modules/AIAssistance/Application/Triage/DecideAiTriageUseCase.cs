using ServiceDesk.Modules.AIAssistance.Application.Contracts;
using ServiceDesk.Modules.AIAssistance.Application.Ports;
using ServiceDesk.Modules.AIAssistance.Domain;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.AIAssistance.Application.Triage;

public interface IDecideAiTriage
{
    Task<TriageDecisionResultDto> ExecuteAsync(
        TriageDecisionCommand command,
        Guid reviewerId,
        CancellationToken cancellationToken = default);
}

internal sealed class DecideAiTriageUseCase(
    ITicketTriageBridge ticketBridge,
    IAiAnalysisStore analysisStore,
    IClock clock) : IDecideAiTriage
{
    public async Task<TriageDecisionResultDto> ExecuteAsync(
        TriageDecisionCommand command,
        Guid reviewerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var analysis = await analysisStore.GetLatestAnalysisAsync(command.TicketId, cancellationToken);
        var reviewer = reviewerId.ToString();
        var now = clock.UtcNow;

        if (command.Decision == TriageDecisionType.Accept)
        {
            Impact impact = Impact.Medium;
            Urgency urgency = Urgency.Medium;
            string? category = null;

            if (analysis is not null && analysis.Succeeded)
            {
                foreach (var s in analysis.Suggestions)
                {
                    if (s.Decision == SuggestionDecision.Pending)
                    {
                        s.Accept(reviewer, now);
                    }

                    if (s.Type == SuggestionType.Category) category = s.SuggestedValue;
                    if (s.Type == SuggestionType.Impact && TriagePolicy.ParseImpact(s.SuggestedValue) is { } parsedImpact)
                        impact = parsedImpact;
                    if (s.Type == SuggestionType.Urgency && TriagePolicy.ParseUrgency(s.SuggestedValue) is { } parsedUrgency)
                        urgency = parsedUrgency;
                }
                await analysisStore.SaveAnalysisAsync(analysis, cancellationToken);
            }

            var applied = await ticketBridge.ApplyTriageDecisionAsync(
                command.TicketId,
                impact,
                urgency,
                category,
                "Chấp nhận gợi ý phân loại của AI",
                reviewerId,
                command.ExpectedRevision,
                cancellationToken);

            return new TriageDecisionResultDto(command.TicketId, command.Decision, applied, "Đã chấp nhận gợi ý phân loại của AI thành công.");
        }

        if (command.Decision == TriageDecisionType.Override)
        {
            if (string.IsNullOrWhiteSpace(command.Reason))
            {
                throw new ArgumentException("Bắt buộc cung cấp lý do khi ghi đè gợi ý AI.", nameof(command.Reason));
            }

            if (!command.Impact.HasValue || !command.Urgency.HasValue)
            {
                throw new ArgumentException("Bắt buộc cung cấp mức độ Ảnh hưởng (Impact) và Khẩn cấp (Urgency) khi ghi đè.");
            }

            if (analysis is not null && analysis.Succeeded)
            {
                foreach (var s in analysis.Suggestions)
                {
                    if (s.Decision == SuggestionDecision.Pending)
                    {
                        var overrideVal = s.Type switch
                        {
                            SuggestionType.Impact => command.Impact.Value.ToString(),
                            SuggestionType.Urgency => command.Urgency.Value.ToString(),
                            SuggestionType.Category => command.CategoryId ?? s.SuggestedValue,
                            _ => s.SuggestedValue
                        };
                        s.Override(overrideVal, reviewer, now);
                    }
                }
                await analysisStore.SaveAnalysisAsync(analysis, cancellationToken);
            }

            var applied = await ticketBridge.ApplyTriageDecisionAsync(
                command.TicketId,
                command.Impact.Value,
                command.Urgency.Value,
                command.CategoryId,
                $"Ghi đè gợi ý AI: {command.Reason.Trim()}",
                reviewerId,
                command.ExpectedRevision,
                cancellationToken);

            return new TriageDecisionResultDto(command.TicketId, command.Decision, applied, "Đã ghi đè phân loại vé thành công.");
        }

        if (command.Decision == TriageDecisionType.Reject)
        {
            if (string.IsNullOrWhiteSpace(command.Reason))
            {
                throw new ArgumentException("Bắt buộc cung cấp lý do khi từ chối gợi ý AI.", nameof(command.Reason));
            }

            if (analysis is not null && analysis.Succeeded)
            {
                foreach (var s in analysis.Suggestions)
                {
                    if (s.Decision == SuggestionDecision.Pending)
                    {
                        s.Reject(reviewer, now);
                    }
                }
                await analysisStore.SaveAnalysisAsync(analysis, cancellationToken);
            }

            return new TriageDecisionResultDto(command.TicketId, command.Decision, false, $"Đã từ chối gợi ý phân loại AI. Lý do: {command.Reason.Trim()}.");
        }

        throw new ArgumentOutOfRangeException(nameof(command.Decision));
    }
}
