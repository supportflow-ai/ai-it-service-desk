namespace ServiceDesk.Application.Common.Interfaces;

/// <summary>
/// Cung cấp thông tin về danh tính và vai trò của người dùng trong request hiện tại.
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// Mã định danh người dùng (User ID), hoặc null nếu chưa xác thực.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Cho biết request hiện tại đã được xác thực hay chưa.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Danh sách vai trò (roles) của người dùng hiện tại.
    /// </summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>
    /// Kiểm tra người dùng có thuộc một vai trò nhất định hay không.
    /// </summary>
    bool IsInRole(string role);
}
