using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Domain.AIAssistance;

/// <summary>
/// Đại diện cho một gợi ý cụ thể từ AI trong một phiên phân tích AIAnalysis.
/// Đề xuất giá trị cho một trường thông tin của vé (Category, Impact, hoặc Urgency).
///
/// Vòng đời quyết định:
/// - Khởi tạo ở trạng thái Pending.
/// - Con người đánh giá chuyển sang Accepted / Overridden / Rejected (trạng thái kết thúc).
/// - Hệ thống tự chuyển sang Expired nếu ticket thay đổi nội dung (stale).
/// </summary>
public class AISuggestion
{
    public Guid Id { get; private set; }

    /// <summary>Khóa ngoại liên kết tới phiên phân tích cha (AIAnalysis).</summary>
    public Guid AnalysisId { get; private set; }

    /// <summary>Loại trường thông tin mà gợi ý này áp dụng.</summary>
    public SuggestionType Type { get; private set; }

    /// <summary>Giá trị do AI đề xuất (ví dụ: "NET", "High").</summary>
    public string SuggestedValue { get; private set; } = string.Empty;

    /// <summary>
    /// Điểm số raw từ model AI, khoảng [0.0, 1.0].
    /// Null nếu provider không trả về điểm số chi tiết từng trường.
    /// </summary>
    public double? ModelScore { get; private set; }

    /// <summary>Trạng thái quyết định hiện tại của gợi ý.</summary>
    public SuggestionDecision Decision { get; private set; } = SuggestionDecision.Pending;

    /// <summary>
    /// Giá trị cuối cùng được áp dụng vào ticket:
    /// - Accepted: bằng SuggestedValue.
    /// - Overridden: giá trị do con người nhập thay thế.
    /// - Rejected / Expired: null.
    /// </summary>
    public string? FinalValue { get; private set; }

    /// <summary>Mã người dùng thực hiện review. Null khi đang ở trạng thái Pending.</summary>
    public string? ReviewedBy { get; private set; }

    /// <summary>Thời điểm thực hiện review. Null khi đang ở trạng thái Pending.</summary>
    public DateTimeOffset? ReviewedAt { get; private set; }

    private AISuggestion() { } // Dành cho EF Core

    /// <summary>
    /// Khởi tạo một gợi ý mới ở trạng thái Pending.
    /// </summary>
    public static AISuggestion Create(
        Guid analysisId,
        SuggestionType type,
        string suggestedValue,
        double? modelScore)
    {
        if (analysisId == Guid.Empty) throw new ArgumentException("Analysis identity is required.", nameof(analysisId));
        ValidateValue(type, suggestedValue);

        if (modelScore.HasValue && !ConfidencePolicy.IsValidScore(modelScore.Value))
            throw new ArgumentException(
                $"Model score {modelScore} is invalid. Must be in [{ConfidencePolicy.MinScore}, {ConfidencePolicy.MaxScore}] and not NaN/Infinity.",
                nameof(modelScore));

        return new AISuggestion
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            Type = type,
            SuggestedValue = suggestedValue,
            ModelScore = modelScore
        };
    }

    /// <summary>
    /// Chấp nhận gợi ý từ AI — FinalValue sẽ bằng SuggestedValue.
    /// </summary>
    public void Accept(string reviewedBy, DateTimeOffset reviewedAt)
    {
        EnsurePending();
        ValidateReviewer(reviewedBy);

        Decision = SuggestionDecision.Accepted;
        FinalValue = SuggestedValue;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
    }

    /// <summary>
    /// Ghi đè gợi ý AI bằng giá trị do con người quyết định.
    /// </summary>
    public void Override(string finalValue, string reviewedBy, DateTimeOffset reviewedAt)
    {
        EnsurePending();
        ValidateReviewer(reviewedBy);

        ValidateValue(Type, finalValue);

        Decision = SuggestionDecision.Overridden;
        FinalValue = finalValue;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
    }

    /// <summary>
    /// Từ chối gợi ý AI — không áp dụng giá trị nào vào ticket.
    /// </summary>
    public void Reject(string reviewedBy, DateTimeOffset reviewedAt)
    {
        EnsurePending();
        ValidateReviewer(reviewedBy);

        Decision = SuggestionDecision.Rejected;
        FinalValue = null;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
    }

    /// <summary>
    /// Đánh dấu gợi ý đã hết hạn (Expired) do ticket đã thay đổi nội dung.
    /// </summary>
    public void MarkExpired()
    {
        if (Decision != SuggestionDecision.Pending)
            return; // Chỉ gợi ý đang chờ (Pending) mới hết hạn; trạng thái kết thúc giữ nguyên.

        Decision = SuggestionDecision.Expired;
        FinalValue = null;
    }

    /// <summary>
    /// Kiểm tra xem gợi ý đã ở trạng thái kết thúc (không thể thay đổi tiếp) hay chưa.
    /// </summary>
    public bool IsTerminal => Decision != SuggestionDecision.Pending;

    private void EnsurePending()
    {
        if (Decision != SuggestionDecision.Pending)
            throw new InvalidOperationException(
                $"Cannot change decision on a suggestion that is already {Decision}.");
    }

    private static void ValidateReviewer(string reviewedBy)
    {
        if (string.IsNullOrWhiteSpace(reviewedBy))
            throw new ArgumentException("ReviewedBy is required.", nameof(reviewedBy));
    }

    private static void ValidateValue(SuggestionType type, string value)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        var names = type switch
        {
            SuggestionType.Category => Enum.GetNames<TicketCategory>(),
            SuggestionType.Impact => Enum.GetNames<Impact>(),
            SuggestionType.Urgency => Enum.GetNames<Urgency>(),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        if (!names.Contains(value, StringComparer.Ordinal))
            throw new ArgumentException("Suggestion value is outside the field's defined values.", nameof(value));
    }
}
