namespace ServiceDesk.Domain.AIAssistance;

/// <summary>
/// Loại trường thông tin mà AI đưa ra gợi ý trong một lần phân tích Triage.
/// Một kết quả phân tích có thể đưa ra nhiều gợi ý khác nhau (Danh mục, Độ ảnh hưởng, Mức khẩn cấp).
/// </summary>
public enum SuggestionType
{
    /// <summary>Gợi ý danh mục sự cố.</summary>
    Category,

    /// <summary>Gợi ý mức độ ảnh hưởng (Impact).</summary>
    Impact,

    /// <summary>Gợi ý mức độ khẩn cấp (Urgency).</summary>
    Urgency
}
