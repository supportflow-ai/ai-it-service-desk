using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Domain.AIAssistance;

/// <summary>
/// Chính sách và rào chắn nghiệp vụ cho tính năng AI Triage.
/// Quy định rõ ràng những gì AI được phép và tuyệt đối không được phép thực hiện trong hệ thống.
/// </summary>
public static class TriagePolicy
{
    /// <summary>
    /// Các danh mục bắt buộc phải do con người phân loại thủ công.
    /// Gợi ý của AI cho các danh mục này chỉ mang tính tham khảo — không bao giờ được tự động áp dụng.
    /// </summary>
    private static readonly HashSet<TicketCategory> ManualTriageCategories = [TicketCategory.SEC];

    /// <summary>
    /// Kiểm tra xem danh mục được chỉ định có bắt buộc phải phân loại thủ công hay không.
    /// </summary>
    public static bool RequiresManualTriage(TicketCategory category)
        => ManualTriageCategories.Contains(category);

    /// <summary>
    /// Kiểm tra xem chuỗi mã danh mục đề xuất có thuộc danh mục đã biết của hệ thống hay không.
    /// </summary>
    public static bool IsKnownCategory(string? categoryCode)
    {
        if (string.IsNullOrWhiteSpace(categoryCode))
            return false;

        return Enum.GetNames<TicketCategory>().Contains(categoryCode.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Chuyển đổi chuỗi mã danh mục sang Enum TicketCategory. Trả về null nếu không hợp lệ.
    /// </summary>
    public static TicketCategory? ParseCategory(string? categoryCode)
    {
        if (string.IsNullOrWhiteSpace(categoryCode))
            return null;

        return Enum.GetNames<TicketCategory>().Contains(categoryCode.Trim(), StringComparer.OrdinalIgnoreCase) && Enum.TryParse<TicketCategory>(categoryCode, ignoreCase: true, out var result)
            ? result
            : null;
    }

    /// <summary>
    /// Chuyển đổi chuỗi giá trị sang Enum Impact. Trả về null nếu không hợp lệ.
    /// </summary>
    public static Impact? ParseImpact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Enum.GetNames<Impact>().Contains(value.Trim(), StringComparer.OrdinalIgnoreCase) && Enum.TryParse<Impact>(value, ignoreCase: true, out var result) ? result : null;
    }

    /// <summary>
    /// Chuyển đổi chuỗi giá trị sang Enum Urgency. Trả về null nếu không hợp lệ.
    /// </summary>
    public static Urgency? ParseUrgency(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Enum.GetNames<Urgency>().Contains(value.Trim(), StringComparer.OrdinalIgnoreCase) && Enum.TryParse<Urgency>(value, ignoreCase: true, out var result) ? result : null;
    }

    /// <summary>
    /// Danh sách các hành vi AI TUYỆT ĐỐI BỊ CẤM thực hiện tự động trong hệ thống.
    /// </summary>
    public static class ForbiddenAIActions
    {
        public const string AutoResolve = "AI không được tự ý resolve ticket.";
        public const string AutoClose = "AI không được tự ý đóng ticket.";
        public const string DeleteResource = "AI không được xóa dữ liệu nghiệp vụ.";
        public const string ResetPassword = "AI không được đặt lại mật khẩu.";
        public const string GrantPermission = "AI không được cấp phát quyền hạn.";
        public const string DeactivateUser = "AI không được vô hiệu hóa người dùng.";
        public const string PublishKB = "AI không được tự xuất bản bài viết tri thức.";
        public const string ChangeSLA = "AI không được thay đổi cấu hình SLA.";
        public const string TransitionTicketState = "AI không được tự ý chuyển trạng thái vé trong vòng đời.";
        public const string SetFinalPriority = "AI không được tự ý gán hoặc quyết định độ ưu tiên cuối cùng của vé.";
    }
}
