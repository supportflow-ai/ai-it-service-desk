using ServiceDesk.Modules.AIAssistance.Application.Contracts;
using ServiceDesk.Modules.AIAssistance.Application.Ports;
using ServiceDesk.Modules.AIAssistance.Domain;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.AIAssistance.Application.Triage;

public interface IDecideAiTriage
{
    Task<TriageDecisionResultDto> ExecuteAsync(TriageDecisionCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Quyết định HITL. Người duyệt luôn là người dùng đã xác thực; quyền xử lý phiếu do Ticketing kiểm khi đọc và khi ghi.
/// Quyết định trên một analysis chạy trong <see cref="IAiAnalysisStore.DecideAsync{TResult}"/>: ghi phiếu, audit và
/// quyết định cùng commit hoặc cùng rollback. Override không kèm analysisId là phân loại thủ công, không đụng analysis nào.
/// </summary>
internal sealed class DecideAiTriageUseCase(
    ITicketTriageBridge ticketBridge,
    IAiAnalysisStore analysisStore,
    ICurrentUser currentUser,
    IClock clock) : IDecideAiTriage
{
    public async Task<TriageDecisionResultDto> ExecuteAsync(TriageDecisionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var reviewerId = AuthenticatedActor.RequireId(currentUser);
        await RequireTriageSnapshotAsync(command.TicketId, cancellationToken);
        var (decision, reason) = Validate(command);

        if (command.AnalysisId is not { } analysisId)
        {
            await ApplyOverrideAsync(command, reason!, cancellationToken);
            return Result(command, reason);
        }

        return await analysisStore.DecideAsync(command.TicketId, analysisId, async analysis =>
        {
            // Kiểm lại quyền và nội dung phiếu bên trong giao dịch, sau khi đã khóa analysis.
            var snapshot = await RequireTriageSnapshotAsync(command.TicketId, cancellationToken);
            if (analysis is null)
                throw new KeyNotFoundException("Không tìm thấy kết quả phân tích AI của vé này.");

            if (analysis.Decision != SuggestionDecision.Pending)
            {
                // Gửi lại đúng quyết định đã commit (ví dụ mất phản hồi): trả kết quả cũ, không ghi lần hai.
                return IsReplay(analysis, command, decision, reason, reviewerId)
                    ? Result(command, reason)
                    : throw new TriageDecisionConflictException("Kết quả phân tích AI này đã có quyết định khác.");
            }

            snapshot = await ticketBridge.GetDecisionSnapshotAsync(command.TicketId, command.ExpectedRevision, cancellationToken)
                ?? throw new KeyNotFoundException("Không tìm thấy phiếu trong phạm vi xử lý.");
            if (!snapshot.CanTriage) throw new UnauthorizedAccessException("Yêu cầu quyền xử lý phiếu.");

            if (!analysis.Succeeded || analysis.Suggestions.Count == 0)
                throw new TriageDecisionConflictException("Kết quả phân tích AI này không có gợi ý để ra quyết định.");
            if (analysis.InputHash != InputHashComputer.Compute(snapshot.Title, snapshot.Description))
                throw new TriageDecisionConflictException("Nội dung vé đã thay đổi sau lần phân tích AI; hãy phân tích lại.");

            TicketTriageChange? change = null;
            if (decision == SuggestionDecision.Accepted)
            {
                var suggested = StoredTriage.ToResult(analysis);
                // Không mặc định Medium: thiếu Impact hoặc Urgency thì người duyệt phải ghi đè hoặc phân loại thủ công.
                if (suggested.SuggestedImpact is not { } impact || suggested.SuggestedUrgency is not { } urgency)
                    throw new TriageDecisionConflictException("Kết quả phân tích AI không có đủ gợi ý Ảnh hưởng và Khẩn cấp; hãy ghi đè hoặc phân loại thủ công.");
                change = await ticketBridge.ApplyTriageDecisionAsync(
                    command.TicketId, impact, urgency, suggested.SuggestedCategory?.ToString(),
                    "Chấp nhận gợi ý phân loại của AI", command.ExpectedRevision, cancellationToken);
            }
            else if (decision == SuggestionDecision.Overridden)
            {
                change = await ApplyOverrideAsync(command, reason!, cancellationToken);
            }

            analysis.RecordDecision(decision, reviewerId, clock.UtcNow, reason, Map(change?.Previous), Map(change?.Applied), command.ExpectedRevision);
            return Result(command, reason);
        }, cancellationToken);
    }

    private async Task<TicketTriageSnapshotDto> RequireTriageSnapshotAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var snapshot = await ticketBridge.GetSnapshotAsync(ticketId, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy vé hỗ trợ có mã {ticketId}.");
        return snapshot.CanTriage
            ? snapshot
            : throw new UnauthorizedAccessException("Bạn không có quyền ra quyết định phân loại cho vé hỗ trợ này.");
    }

    private static (SuggestionDecision Decision, string? Reason) Validate(TriageDecisionCommand command)
    {
        var decision = command.Decision switch
        {
            TriageDecisionType.Accept => SuggestionDecision.Accepted,
            TriageDecisionType.Override => SuggestionDecision.Overridden,
            TriageDecisionType.Reject => SuggestionDecision.Rejected,
            _ => throw new ArgumentException("Loại quyết định không hợp lệ.", nameof(command))
        };

        if (decision != SuggestionDecision.Overridden && command.AnalysisId is null)
            throw new ArgumentException("Bắt buộc chỉ rõ analysisId của kết quả phân tích đang xem.", nameof(command));
        if (decision != SuggestionDecision.Rejected && command.ExpectedRevision is null or 0)
            throw new ArgumentException("Expected revision is required.", nameof(command));
        if (decision == SuggestionDecision.Accepted) return (decision, null);

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            throw new ArgumentException(decision == SuggestionDecision.Overridden
                ? "Bắt buộc cung cấp lý do khi ghi đè gợi ý AI."
                : "Bắt buộc cung cấp lý do khi từ chối gợi ý AI.", nameof(command));
        }

        if (decision == SuggestionDecision.Overridden && (command.Impact is null || command.Urgency is null))
            throw new ArgumentException("Bắt buộc cung cấp mức độ Ảnh hưởng (Impact) và Khẩn cấp (Urgency) khi ghi đè.");
        return (decision, command.Reason.Trim());
    }

    private Task<TicketTriageChange> ApplyOverrideAsync(TriageDecisionCommand command, string reason, CancellationToken cancellationToken) =>
        ticketBridge.ApplyTriageDecisionAsync(
            command.TicketId, command.Impact!.Value, command.Urgency!.Value, command.CategoryId,
            $"Ghi đè gợi ý AI: {reason}", command.ExpectedRevision, cancellationToken);

    private static bool IsReplay(AIAnalysis analysis, TriageDecisionCommand command, SuggestionDecision decision, string? reason, Guid reviewerId) =>
        analysis.Decision == decision &&
        analysis.DecidedBy == reviewerId &&
        analysis.DecisionRevision == command.ExpectedRevision &&
        analysis.DecisionReason == reason &&
        (decision != SuggestionDecision.Overridden ||
            (analysis.Applied?.Impact == command.Impact.ToString() &&
             analysis.Applied?.Urgency == command.Urgency.ToString() &&
             analysis.Applied?.CategoryId == (command.CategoryId ?? analysis.Previous?.CategoryId)));

    private static TriageValues? Map(TicketTriageValues? values) =>
        values is null ? null : new TriageValues(values.CategoryId, values.Impact, values.Urgency, values.Priority);

    private static TriageDecisionResultDto Result(TriageDecisionCommand command, string? reason) =>
        new(command.TicketId, command.Decision, command.Decision != TriageDecisionType.Reject, command.Decision switch
        {
            TriageDecisionType.Accept => "Đã chấp nhận gợi ý phân loại của AI thành công.",
            TriageDecisionType.Override => "Đã ghi đè phân loại vé thành công.",
            _ => $"Đã từ chối gợi ý phân loại AI. Lý do: {reason}."
        });
}

/// <summary>Analysis không cho phép quyết định (thất bại, đã cũ, đã quyết định khác hoặc thiếu gợi ý) → 409.</summary>
internal sealed class TriageDecisionConflictException(string message) : ConflictException(message);
