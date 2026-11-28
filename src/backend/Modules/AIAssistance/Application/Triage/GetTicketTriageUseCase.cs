using ServiceDesk.Modules.AIAssistance.Application.Contracts;
using ServiceDesk.Modules.AIAssistance.Application.Ports;
using ServiceDesk.Modules.AIAssistance.Domain;
using ServiceDesk.Modules.Ticketing.Application.Contracts;

namespace ServiceDesk.Modules.AIAssistance.Application.Triage;

/// <summary>Đúng một trong hai hình chiếu có giá trị, theo quyền xử lý phiếu mà Ticketing trả về.</summary>
public sealed record TicketTriageReadResult(AgentTriageView? StaffView, RequesterTriageView? RequesterView);

public interface IGetTicketTriage
{
    Task<TicketTriageReadResult> ExecuteAsync(Guid ticketId, CancellationToken cancellationToken = default);
}

/// <summary>Truy vấn thuần: chỉ đọc analysis đã lưu, không gọi dịch vụ AI và không ghi dữ liệu.</summary>
internal sealed class GetTicketTriageUseCase(
    IAiAnalysisStore analysisStore,
    ITicketTriageBridge ticketBridge) : IGetTicketTriage
{
    public async Task<TicketTriageReadResult> ExecuteAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var snapshot = await ticketBridge.GetSnapshotAsync(ticketId, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy vé hỗ trợ có mã {ticketId}.");

        var analysis = await analysisStore.GetLatestAsync(ticketId, cancellationToken);
        var view = AgentTriageView.FromTriageResult(StoredTriage.ToResult(analysis));
        if (analysis is not null)
            view = view.WithAnalysis(analysis, analysis.InputHash != InputHashComputer.Compute(snapshot.Title, snapshot.Description));
        return snapshot.CanTriage
            ? new TicketTriageReadResult(view, null)
            : new TicketTriageReadResult(null, RequesterTriageView.FromAgentView(view));
    }
}

/// <summary>Dựng lại kết quả phân loại từ analysis đã lưu; giá trị không đọc được coi như không có gợi ý.</summary>
internal static class StoredTriage
{
    public static TriageResult ToResult(AIAnalysis? analysis)
    {
        if (analysis is null) return TriageResult.Unavailable("Chưa có kết quả phân loại AI cho vé hỗ trợ này.");
        if (!analysis.Succeeded) return TriageResult.RestoreFailure(analysis.Outcome, analysis.FailureReason ?? "Phân loại AI thất bại.", analysis.ValidationErrors);

        var category = Find(analysis, SuggestionType.Category, TriagePolicy.ParseCategory);
        var impact = Find(analysis, SuggestionType.Impact, TriagePolicy.ParseImpact);
        var urgency = Find(analysis, SuggestionType.Urgency, TriagePolicy.ParseUrgency);
        return TriageResult.Success(category.Value, impact.Value, urgency.Value, category.Score, impact.Score, urgency.Score);
    }

    private static (T? Value, double? Score) Find<T>(AIAnalysis analysis, SuggestionType type, Func<string?, T?> parse) where T : struct
    {
        var suggestion = analysis.Suggestions.FirstOrDefault(s => s.Type == type);
        var value = parse(suggestion?.SuggestedValue);
        return value is null ? (null, null) : (value, suggestion!.ModelScore);
    }
}
