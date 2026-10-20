using ServiceDesk.Domain.AIAssistance;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.AIAssistance.Contracts;

/// <summary>
/// Kết quả phân loại tự động của AI Triage.
/// Bao gồm trạng thái thực thi (Outcome), các gợi ý và điểm số tin cậy tương ứng.
/// </summary>
public sealed record TriageResult
{
    private TriageResult() { }

    /// <summary>Kết quả chung của lần phân loại AI.</summary>
    public TriageOutcome Outcome { get; private init; }

    /// <summary>Danh mục AI đề xuất.</summary>
    public TicketCategory? SuggestedCategory { get; private init; }

    /// <summary>Mức độ ảnh hưởng (Impact) AI đề xuất.</summary>
    public Impact? SuggestedImpact { get; private init; }

    /// <summary>Mức độ khẩn cấp (Urgency) AI đề xuất.</summary>
    public Urgency? SuggestedUrgency { get; private init; }

    /// <summary>Điểm số raw [0.0, 1.0] cho gợi ý danh mục.</summary>
    public double? CategoryScore { get; private init; }

    /// <summary>Điểm số raw [0.0, 1.0] cho gợi ý Impact.</summary>
    public double? ImpactScore { get; private init; }

    /// <summary>Điểm số raw [0.0, 1.0] cho gợi ý Urgency.</summary>
    public double? UrgencyScore { get; private init; }

    /// <summary>Mức tin cậy (Low, Medium, High) cho gợi ý danh mục.</summary>
    public ConfidenceBand? CategoryConfidence => ConfidencePolicy.Classify(CategoryScore);

    /// <summary>Mức tin cậy (Low, Medium, High) cho gợi ý Impact.</summary>
    public ConfidenceBand? ImpactConfidence => ConfidencePolicy.Classify(ImpactScore);

    /// <summary>Mức tin cậy (Low, Medium, High) cho gợi ý Urgency.</summary>
    public ConfidenceBand? UrgencyConfidence => ConfidencePolicy.Classify(UrgencyScore);

    /// <summary>Cho biết danh mục đề xuất có bắt buộc phải phân loại thủ công hay không.</summary>
    public bool RequiresManualTriage =>
        SuggestedCategory.HasValue && TriagePolicy.RequiresManualTriage(SuggestedCategory.Value);

    /// <summary>Thông tin nguyên nhân nếu phân loại thất bại hoặc không khả dụng.</summary>
    public string? FailureReason { get; private init; }

    /// <summary>Danh sách lỗi xác thực dữ liệu đầu ra từ AI nếu có.</summary>
    public IReadOnlyList<string> ValidationErrors { get; private init; } = [];

    /// <summary>Khởi tạo kết quả phân loại thành công.</summary>
    public static TriageResult Success(
        TicketCategory? category,
        Impact? impact,
        Urgency? urgency,
        double? categoryScore = null,
        double? impactScore = null,
        double? urgencyScore = null)
    {
        ValidateSuggestion(category, categoryScore);
        ValidateSuggestion(impact, impactScore);
        ValidateSuggestion(urgency, urgencyScore);
        return new TriageResult
        {
            Outcome = TriageOutcome.Success,
            SuggestedCategory = category,
            SuggestedImpact = impact,
            SuggestedUrgency = urgency,
            CategoryScore = categoryScore,
            ImpactScore = impactScore,
            UrgencyScore = urgencyScore
        };
    }

    /// <summary>Khởi tạo kết quả khi dịch vụ AI không khả dụng (luồng chính vẫn tiếp tục bình thường).</summary>
    public static TriageResult Unavailable(string reason = "AI classification service is unavailable.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new TriageResult
        {
            Outcome = TriageOutcome.Unavailable,
            FailureReason = reason
        };
    }

    /// <summary>Khởi tạo kết quả khi kết nối AI bị quá thời gian chờ (Timeout).</summary>
    public static TriageResult Timeout(string reason = "AI classification timed out.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new TriageResult
        {
            Outcome = TriageOutcome.Timeout,
            FailureReason = reason
        };
    }

    /// <summary>Khởi tạo kết quả khi AI trả về phản hồi không đúng cấu trúc / không hợp lệ.</summary>
    public static TriageResult InvalidResponse(string reason, IReadOnlyList<string>? validationErrors = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new TriageResult
        {
            Outcome = TriageOutcome.InvalidResponse,
            FailureReason = reason,
            ValidationErrors = Array.AsReadOnly(validationErrors?.ToArray() ?? [])
        };
    }

    private static void ValidateSuggestion<T>(T? value, double? score) where T : struct, Enum
    {
        if (value.HasValue && !Enum.IsDefined(value.Value))
            throw new ArgumentOutOfRangeException(nameof(value));
        if (score.HasValue && (!value.HasValue || !ConfidencePolicy.IsValidScore(score.Value)))
            throw new ArgumentException("A finite score in [0,1] requires its corresponding suggestion.", nameof(score));
    }
}

/// <summary>Trạng thái kết quả của một lần thực thi AI Triage.</summary>
public enum TriageOutcome
{
    /// <summary>Phân loại thành công.</summary>
    Success,

    /// <summary>Dịch vụ AI không khả dụng.</summary>
    Unavailable,

    /// <summary>Quá thời gian chờ phản hồi từ AI.</summary>
    Timeout,

    /// <summary>Phản hồi từ AI không đúng cấu trúc hợp lệ.</summary>
    InvalidResponse,

    /// <summary>Dữ liệu đề xuất không vượt qua kiểm tra tính hợp lệ.</summary>
    ValidationFailed
}
