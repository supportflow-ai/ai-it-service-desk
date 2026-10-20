namespace ServiceDesk.Domain.Identity;

/// <summary>
/// Các hằng số định danh vai trò (Role) cốt lõi trong toàn hệ thống.
/// Định nghĩa thuần C#, không phụ thuộc ASP.NET Identity hay framework ngoài.
/// </summary>
public static class RoleNames
{
    /// <summary>Quản trị viên hệ thống (toàn quyền quản trị người dùng và điều phối vé).</summary>
    public const string Admin = "Admin";

    /// <summary>Kỹ thuật viên IT / Nhân viên hỗ trợ (tiếp nhận và giải quyết yêu cầu).</summary>
    public const string Agent = "Agent";

    /// <summary>Người dùng nội bộ gửi yêu cầu hỗ trợ IT.</summary>
    public const string Requester = "Requester";

    /// <summary>Danh sách tất cả vai trò hợp lệ trong hệ thống.</summary>
    public static readonly IReadOnlyList<string> All = [Admin, Agent, Requester];
}
