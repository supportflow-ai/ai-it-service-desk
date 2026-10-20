namespace ServiceDesk.Application.AIAssistance.Contracts;

/// <summary>
/// Dữ liệu đầu vào rút gọn gửi tới dịch vụ phân loại AI (Triage).
/// Chỉ bao gồm các trường cần thiết — không chứa dữ liệu nhạy cảm hay metadata nội bộ.
/// </summary>
public sealed record TriageRequest
{
    /// <summary>Mã ticket cần phân tích.</summary>
    public required Guid TicketId { get; init; }

    /// <summary>
    /// Số revision của ticket tại thời điểm gửi yêu cầu.
    /// Dùng để phát hiện nếu ticket bị sửa đổi trong lúc đang chờ AI phân tích.
    /// </summary>
    public required long TicketRevision { get; init; }

    /// <summary>Tiêu đề ticket — tín hiệu phân loại chính.</summary>
    public required string Title { get; init; }

    /// <summary>Nội dung mô tả ticket — tín hiệu phân loại phụ trợ.</summary>
    public required string Description { get; init; }
}
