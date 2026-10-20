namespace ServiceDesk.Domain.Ticketing;

/// <summary>
/// Mức độ ưu tiên xử lý của vé (P1 đến P4).
/// Độ ưu tiên LUÔN ĐƯỢC TÍNH TOÁN tự động từ Impact × Urgency theo chuẩn ITIL.
/// Phía Client hoặc AI tuyệt đối không được phép tự gán trực tiếp Priority.
/// </summary>
public enum Priority
{
    /// <summary>P1 - Khẩn cấp cực độ (Critical): Cần can thiệp ngay lập tức.</summary>
    P1,

    /// <summary>P2 - Ưu tiên cao (High): Ảnh hưởng lớn, cần xử lý trong thời gian ngắn.</summary>
    P2,

    /// <summary>P3 - Ưu tiên trung bình (Medium): Ảnh hưởng vừa phải, xử lý theo luồng tiêu chuẩn.</summary>
    P3,

    /// <summary>P4 - Ưu tiên thấp (Low): Ảnh hưởng nhỏ, xử lý theo kế hoạch thông thường.</summary>
    P4
}
