namespace ServiceDesk.Domain.Ticketing;

/// <summary>
/// Mức độ khẩn cấp về thời gian cần xử lý sự cố (Urgency).
/// Dùng để tính toán độ ưu tiên: Priority = Impact × Urgency.
/// </summary>
public enum Urgency
{
    /// <summary>Khẩn cấp thấp (có phương án thay thế, có thể xử lý theo lịch).</summary>
    Low,

    /// <summary>Khẩn cấp trung bình (cần xử lý sớm trong ngày để không phát sinh rủi ro).</summary>
    Medium,

    /// <summary>Khẩn cấp cao (cần xử lý ngay lập tức, đình trệ quy trình nghiêm trọng).</summary>
    High
}
