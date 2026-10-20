namespace ServiceDesk.Domain.Identity;

/// <summary>
/// Các hằng số định danh chính sách phân quyền (Authorization Policies) sử dụng cho API endpoints.
/// </summary>
public static class PolicyNames
{
    /// <summary>Yêu cầu quyền Quản trị viên (Admin).</summary>
    public const string RequireAdmin = "RequireAdmin";

    /// <summary>Yêu cầu quyền Kỹ thuật viên (Agent) hoặc Quản trị viên (Admin).</summary>
    public const string RequireAgent = "RequireAgent";

    /// <summary>Yêu cầu người dùng có ít nhất một vai trò hợp lệ trong hệ thống.</summary>
    public const string RequireRequester = "RequireRequester";
}
