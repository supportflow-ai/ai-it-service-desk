using ServiceDesk.Application.Common.Interfaces;

namespace ServiceDesk.Application.Common;

/// <summary>
/// Đại diện cho người dùng ẩn danh (chưa đăng nhập). Thường dùng cho background worker hoặc tác vụ không có ngữ cảnh HTTP.
/// </summary>
public sealed class AnonymousCurrentUser : ICurrentUser
{
    public string? UserId => null;
    public bool IsAuthenticated => false;
    public IReadOnlyList<string> Roles => [];
    public bool IsInRole(string role) => false;
}
