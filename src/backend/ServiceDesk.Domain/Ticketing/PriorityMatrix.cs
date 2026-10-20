namespace ServiceDesk.Domain.Ticketing;

/// <summary>
/// Ma trận tính toán độ ưu tiên (Priority) tất định từ Impact × Urgency.
/// Đây là quy tắc nghiệp vụ cốt lõi của Domain, không có sự can thiệp của AI.
///
/// Ma trận chuẩn ITIL (3×3):
///              Urgency
///              High    Medium  Low
/// Impact High   P1      P2     P3
///        Med    P2      P3     P4
///        Low    P3      P4     P4
/// </summary>
public static class PriorityMatrix
{
    /// <summary>
    /// Tính toán mức độ ưu tiên P1 - P4 từ mức độ ảnh hưởng (Impact) và mức độ khẩn cấp (Urgency).
    /// </summary>
    public static Priority Calculate(Impact impact, Urgency urgency)
    {
        return (impact, urgency) switch
        {
            (Impact.High, Urgency.High) => Priority.P1,
            (Impact.High, Urgency.Medium) => Priority.P2,
            (Impact.High, Urgency.Low) => Priority.P3,

            (Impact.Medium, Urgency.High) => Priority.P2,
            (Impact.Medium, Urgency.Medium) => Priority.P3,
            (Impact.Medium, Urgency.Low) => Priority.P4,

            (Impact.Low, Urgency.High) => Priority.P3,
            (Impact.Low, Urgency.Medium) => Priority.P4,
            (Impact.Low, Urgency.Low) => Priority.P4,

            _ => throw new ArgumentOutOfRangeException(
                $"Giá trị Impact ({impact}) hoặc Urgency ({urgency}) không hợp lệ.")
        };
    }
}
