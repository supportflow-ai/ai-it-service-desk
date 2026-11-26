using System.Text.Json.Serialization;

namespace ServiceDesk.Modules.Identity.Application.Contracts;

/// <summary>
/// Thông tin tài khoản người dùng và danh sách các vai trò (Roles) được cấp quyền.
/// </summary>
public sealed record UserInfo
{
    /// <summary>Mã định danh duy nhất của người dùng.</summary>
    public required string UserId { get; init; }

    /// <summary>Địa chỉ email tài khoản.</summary>
    public required string Email { get; init; }

    /// <summary>Họ và tên người dùng.</summary>
    public string? FullName { get; init; }

    /// <summary>Số điện thoại liên hệ của người dùng.</summary>
    public string? PhoneNumber { get; init; }

    /// <summary>Danh sách các vai trò (Requester, Agent, Admin).</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>Dấu bảo mật hiện tại của tài khoản để phát hiện phiên/JWT đã bị thu hồi.</summary>
    [JsonIgnore]
    public string? SecurityStamp { get; init; }
}
