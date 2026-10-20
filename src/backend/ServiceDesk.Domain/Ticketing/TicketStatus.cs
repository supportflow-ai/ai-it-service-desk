namespace ServiceDesk.Domain.Ticketing;

/// <summary>
/// Trạng thái của vé trong vòng đời xử lý (State Machine).
/// Thứ tự số nguyên được giữ cố định để bảo toàn dữ liệu persisted trong database.
/// </summary>
public enum TicketStatus
{
    /// <summary>Bản nháp, chưa gửi chính thức.</summary>
    Draft = 0,

    /// <summary>Đã gửi, đang chờ tiếp nhận và phân công.</summary>
    Submitted = 1,

    /// <summary>Đang được kỹ thuật viên xử lý.</summary>
    InProgress = 2,

    /// <summary>Đang chờ người dùng phản hồi thông tin bổ sung.</summary>
    PendingUser = 3,

    /// <summary>Đã xử lý xong, chờ người dùng nghiệm thu.</summary>
    Resolved = 4,

    /// <summary>Đã nghiệm thu và đóng hoàn tất (trạng thái kết thúc).</summary>
    Closed = 5,

    /// <summary>Đã hủy yêu cầu (trạng thái kết thúc).</summary>
    Cancelled = 6,

    /// <summary>Đã phân loại danh mục và độ ưu tiên sơ bộ.</summary>
    Triaged = 7,

    /// <summary>Đã phân công cho kỹ thuật viên hoặc đội hỗ trợ.</summary>
    Assigned = 8,

    /// <summary>Đang tạm dừng chờ đối tác / bên thứ ba xử lý.</summary>
    PendingExternal = 9
}
