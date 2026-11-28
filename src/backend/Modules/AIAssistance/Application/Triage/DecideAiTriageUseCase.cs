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
/// Phiếu được ghi trước, gợi ý chỉ được đánh dấu sau khi ghi phiếu thành công.
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
        var reviewer = AuthenticatedActor.RequireId(currentUser).ToString();
        var snapshot = await ticketBridge.GetSnapshotAsync(command.TicketId, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy vé hỗ trợ có mã {command.TicketId}.");
        if (!snapshot.CanTriage)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền ra quyết định phân loại cho vé hỗ trợ này.");
        }

        var analysis = await analysisStore.GetLatestAnalysisAsync(command.TicketId, cancellationToken);
        var now = clock.UtcNow;

        switch (command.Decision)
        {
            case TriageDecisionType.Accept:
            {
                var pending = RequireDecidableAnalysis(analysis, snapshot);
                var suggested = StoredTriage.ToResult(pending);
                // Không mặc định Medium: thiếu Impact hoặc Urgency thì người duyệt phải ghi đè hoặc phân loại thủ công.
                if (suggested.SuggestedImpact is not { } impact || suggested.SuggestedUrgency is not { } urgency)
                    throw new TriageDecisionConflictException("Kết quả phân tích AI không có đủ gợi ý Ảnh hưởng và Khẩn cấp; hãy ghi đè hoặc phân loại thủ công.");

                await ticketBridge.ApplyTriageDecisionAsync(
                    command.TicketId, impact, urgency, suggested.SuggestedCategory?.ToString(),
                    "Chấp nhận gợi ý phân loại của AI", command.ExpectedRevision, cancellationToken);
                foreach (var s in pending.Suggestions) s.Accept(reviewer, now);
                await analysisStore.SaveAnalysisAsync(pending, cancellationToken);
                return new TriageDecisionResultDto(command.TicketId, command.Decision, true, "Đã chấp nhận gợi ý phân loại của AI thành công.");
            }

            case TriageDecisionType.Override:
            {
                var reason = RequireReason(command.Reason, "Bắt buộc cung cấp lý do khi ghi đè gợi ý AI.");
                if (command.Impact is not { } impact || command.Urgency is not { } urgency)
                {
                    throw new ArgumentException("Bắt buộc cung cấp mức độ Ảnh hưởng (Impact) và Khẩn cấp (Urgency) khi ghi đè.");
                }

                await ticketBridge.ApplyTriageDecisionAsync(
                    command.TicketId, impact, urgency, command.CategoryId,
                    $"Ghi đè gợi ý AI: {reason}", command.ExpectedRevision, cancellationToken);

                // Ghi đè không cần analysis (phân loại thủ công); chỉ ghi lịch sử khi analysis hiện tại còn chờ quyết định.
                if (IsDecidable(analysis, snapshot))
                {
                    var category = command.CategoryId ?? TriagePolicy.ParseCategory(snapshot.CategoryId)?.ToString();
                    foreach (var s in analysis!.Suggestions)
                    {
                        var finalValue = s.Type switch
                        {
                            SuggestionType.Impact => impact.ToString(),
                            SuggestionType.Urgency => urgency.ToString(),
                            _ => category
                        };
                        // Mã danh mục cũ ngoài enum không làm giá trị cuối được: không được lỗi sau khi phiếu đã ghi.
                        if (finalValue is null) s.Reject(reviewer, now);
                        else s.Override(finalValue, reviewer, now);
                    }
                    await analysisStore.SaveAnalysisAsync(analysis, cancellationToken);
                }

                return new TriageDecisionResultDto(command.TicketId, command.Decision, true, "Đã ghi đè phân loại vé thành công.");
            }

            case TriageDecisionType.Reject:
            {
                var reason = RequireReason(command.Reason, "Bắt buộc cung cấp lý do khi từ chối gợi ý AI.");
                var pending = RequireDecidableAnalysis(analysis, snapshot);
                foreach (var s in pending.Suggestions) s.Reject(reviewer, now);
                await analysisStore.SaveAnalysisAsync(pending, cancellationToken);
                return new TriageDecisionResultDto(command.TicketId, command.Decision, false, $"Đã từ chối gợi ý phân loại AI. Lý do: {reason}.");
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(command), "Loại quyết định không hợp lệ.");
        }
    }

    private static string RequireReason(string? reason, string message) =>
        string.IsNullOrWhiteSpace(reason) ? throw new ArgumentException(message, nameof(reason)) : reason.Trim();

    private static bool IsDecidable(AIAnalysis? analysis, TicketTriageSnapshotDto snapshot) =>
        analysis is { Succeeded: true } &&
        analysis.Suggestions.Count > 0 &&
        analysis.Suggestions.All(s => !s.IsTerminal) &&
        analysis.InputHash == InputHashComputer.Compute(snapshot.Title, snapshot.Description);

    private static AIAnalysis RequireDecidableAnalysis(AIAnalysis? analysis, TicketTriageSnapshotDto snapshot)
    {
        if (analysis is not { Succeeded: true } || analysis.Suggestions.Count == 0)
            throw new TriageDecisionConflictException("Chưa có kết quả phân tích AI thành công để ra quyết định.");
        if (analysis.InputHash != InputHashComputer.Compute(snapshot.Title, snapshot.Description))
            throw new TriageDecisionConflictException("Nội dung vé đã thay đổi sau lần phân tích AI; hãy phân tích lại.");
        if (analysis.Suggestions.Any(s => s.IsTerminal))
            throw new TriageDecisionConflictException("Kết quả phân tích AI này đã có quyết định.");
        return analysis;
    }
}

/// <summary>Analysis không cho phép quyết định (không có, thất bại, đã cũ, đã quyết định hoặc gợi ý không hợp lệ) → 409.</summary>
internal sealed class TriageDecisionConflictException(string message) : ConflictException(message);
