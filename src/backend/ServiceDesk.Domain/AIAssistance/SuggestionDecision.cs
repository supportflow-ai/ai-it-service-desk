namespace ServiceDesk.Domain.AIAssistance;

/// <summary>
/// Trạng thái quyết định của con người đối với từng gợi ý từ AI.
/// Vòng đời: Khởi tạo ở Pending.
/// Accepted, Overridden, Rejected, Expired là các trạng thái kết thúc (terminal) — không thể chuyển đổi tiếp.
/// </summary>
public enum SuggestionDecision
{
    /// <summary>Đang chờ con người xem xét và phê duyệt.</summary>
    Pending,

    /// <summary>Chấp nhận gợi ý: Giá trị cuối cùng = Giá trị AI đề xuất.</summary>
    Accepted,

    /// <summary>Ghi đè gợi ý: Con người tự nhập giá trị thay thế theo nhận định thực tế.</summary>
    Overridden,

    /// <summary>Từ chối gợi ý: Không áp dụng bất kỳ giá trị nào từ AI.</summary>
    Rejected,

    /// <summary>Hết hạn do ticket đã thay đổi nội dung kể từ lúc AI phân tích (dữ liệu stale).</summary>
    Expired
}
