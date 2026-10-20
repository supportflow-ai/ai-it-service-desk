using ServiceDesk.Domain.AIAssistance;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.AIAssistance.Contracts;

/// <summary>
/// Hình chiếu an toàn của kết quả AI Triage dành cho người yêu cầu (Requester).
/// Requester CHỈ ĐƯỢC XEM:
/// - Gợi ý danh mục + mức độ tin cậy
/// - Cảnh báo cần phân loại thủ công
///
/// Requester KHÔNG ĐƯỢC XEM:
/// - Gợi ý mức độ ảnh hưởng / khẩn cấp (thuộc thẩm quyền kỹ thuật viên)
/// - Điểm số raw từ model
/// </summary>
public sealed record RequesterTriageView
{
    public required TriageOutcome Outcome { get; init; }
    public TicketCategory? SuggestedCategory { get; init; }
    public ConfidenceBand? CategoryConfidence { get; init; }
    public bool RequiresManualTriage { get; init; }
    public string? FailureReason { get; init; }

    public static RequesterTriageView FromTriageResult(TriageResult result)
    {
        return new RequesterTriageView
        {
            Outcome = result.Outcome,
            SuggestedCategory = result.SuggestedCategory,
            CategoryConfidence = result.CategoryConfidence,
            RequiresManualTriage = result.RequiresManualTriage,
            FailureReason = result.FailureReason
        };
    }
}

/// <summary>
/// Hình chiếu kết quả AI Triage đầy đủ dành cho kỹ thuật viên / quản trị viên (Agent, Admin).
/// Hiển thị toàn bộ thông tin đề xuất, điểm số tin cậy và lỗi xác thực nếu có.
/// </summary>
public sealed record AgentTriageView
{
    public required TriageOutcome Outcome { get; init; }
    public TicketCategory? SuggestedCategory { get; init; }
    public Impact? SuggestedImpact { get; init; }
    public Urgency? SuggestedUrgency { get; init; }
    public double? CategoryScore { get; init; }
    public double? ImpactScore { get; init; }
    public double? UrgencyScore { get; init; }
    public ConfidenceBand? CategoryConfidence { get; init; }
    public ConfidenceBand? ImpactConfidence { get; init; }
    public ConfidenceBand? UrgencyConfidence { get; init; }
    public bool RequiresManualTriage { get; init; }
    public string? FailureReason { get; init; }
    public IReadOnlyList<string> ValidationErrors { get; init; } = [];

    public static AgentTriageView FromTriageResult(TriageResult result)
    {
        return new AgentTriageView
        {
            Outcome = result.Outcome,
            SuggestedCategory = result.SuggestedCategory,
            SuggestedImpact = result.SuggestedImpact,
            SuggestedUrgency = result.SuggestedUrgency,
            CategoryScore = result.CategoryScore,
            ImpactScore = result.ImpactScore,
            UrgencyScore = result.UrgencyScore,
            CategoryConfidence = result.CategoryConfidence,
            ImpactConfidence = result.ImpactConfidence,
            UrgencyConfidence = result.UrgencyConfidence,
            RequiresManualTriage = result.RequiresManualTriage,
            FailureReason = result.FailureReason,
            ValidationErrors = result.ValidationErrors
        };
    }
}
