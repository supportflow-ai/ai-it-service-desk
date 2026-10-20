using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ServiceDesk.Application.Common.Interfaces;

namespace ServiceDesk.Api.Identity;

/// <summary>
/// Đọc thông tin và quyền hạn của người dùng hiện tại từ Claims trong HttpContext.
/// Được đăng ký ở mức Scoped — một instance cho mỗi HTTP request.
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>Mã định danh người dùng (NameIdentifier claim).</summary>
    public string? UserId =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>Xác định người dùng đã đăng nhập hợp lệ hay chưa.</summary>
    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    /// <summary>Danh sách các vai trò (roles) đọc từ Role claims.</summary>
    public IReadOnlyList<string> Roles =>
        _httpContextAccessor.HttpContext?.User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList()
        ?? (IReadOnlyList<string>)[];

    /// <summary>Kiểm tra người dùng có vai trò cụ thể hay không.</summary>
    public bool IsInRole(string role) =>
        _httpContextAccessor.HttpContext?.User?.IsInRole(role) ?? false;
}
