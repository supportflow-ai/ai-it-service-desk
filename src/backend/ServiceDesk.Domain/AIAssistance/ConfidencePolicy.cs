namespace ServiceDesk.Domain.AIAssistance;

/// <summary>
/// Chính sách phân loại điểm số tin cậy (Confidence Score) từ model AI sang các mức hiển thị (ConfidenceBand).
/// Các ngưỡng được tập trung tại đây để đảm bảo tính nhất quán trên toàn hệ thống.
/// </summary>
public static class ConfidencePolicy
{
    /// <summary>Điểm số model tối thiểu hợp lệ [0.0, 1.0].</summary>
    public const double MinScore = 0.0;

    /// <summary>Điểm số model tối đa hợp lệ [0.0, 1.0].</summary>
    public const double MaxScore = 1.0;

    /// <summary>Điểm số dưới ngưỡng này được xếp vào mức độ tin cậy thấp (Low: < 0.4).</summary>
    public const double LowThreshold = 0.4;

    /// <summary>Điểm số từ ngưỡng này trở lên được xếp vào mức độ tin cậy cao (High: >= 0.75).</summary>
    public const double HighThreshold = 0.75;

    /// <summary>
    /// Phân loại điểm số raw từ model thành nhóm tin cậy (ConfidenceBand).
    /// Trả về null nếu điểm số không hợp lệ (NaN, Infinity, ngoài khoảng [0, 1]).
    /// </summary>
    public static ConfidenceBand? Classify(double? score)
    {
        if (score is null)
            return null;

        if (!IsValidScore(score.Value))
            return null;

        return score.Value switch
        {
            < LowThreshold => ConfidenceBand.Low,
            >= HighThreshold => ConfidenceBand.High,
            _ => ConfidenceBand.Medium
        };
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ của điểm số: nằm trong khoảng [0, 1] và không phải NaN / Infinity.
    /// </summary>
    public static bool IsValidScore(double score)
    {
        if (double.IsNaN(score) || double.IsInfinity(score))
            return false;

        return score >= MinScore && score <= MaxScore;
    }
}
