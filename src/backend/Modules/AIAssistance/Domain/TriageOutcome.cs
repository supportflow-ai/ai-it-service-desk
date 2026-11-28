namespace ServiceDesk.Modules.AIAssistance.Domain;

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
