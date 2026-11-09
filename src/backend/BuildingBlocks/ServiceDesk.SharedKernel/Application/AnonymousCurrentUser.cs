using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.SharedKernel.Application;

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
