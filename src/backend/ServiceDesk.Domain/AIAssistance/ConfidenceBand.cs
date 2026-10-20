namespace ServiceDesk.Domain.AIAssistance;

/// <summary>
/// Mức độ tin cậy của gợi ý AI hiển thị cho người dùng (Low, Medium, High).
/// Được suy diễn từ điểm số raw của model thông qua ConfidencePolicy.
/// Lưu ý: Đây là nhãn phân nhóm hỗ trợ người dùng ưu tiên xem xét, không phải xác suất thống kê tuyệt đối.
/// </summary>
public enum ConfidenceBand
{
    /// <summary>Độ tin cậy thấp — cần xem xét kỹ lưỡng trước khi áp dụng.</summary>
    Low,

    /// <summary>Độ tin cậy trung bình — khuyến nghị kỹ thuật viên rà soát.</summary>
    Medium,

    /// <summary>Độ tin cậy cao — khả năng chính xác cao nhưng quyết định cuối cùng vẫn thuộc về con người.</summary>
    High
}
