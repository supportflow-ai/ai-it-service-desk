namespace ServiceDesk.Application.Identity.Contracts;

/// <summary>
/// Dữ liệu đầu vào yêu cầu đăng ký tài khoản người dùng mới.
/// </summary>
public sealed record RegisterRequest
{
    /// <summary>Địa chỉ email đăng ký (sử dụng làm tài khoản đăng nhập).</summary>
    public required string Email { get; init; }

    /// <summary>Mật khẩu đăng ký.</summary>
    public required string Password { get; init; }

    /// <summary>Họ và tên đầy đủ của người dùng.</summary>
    public string? FullName { get; init; }

    /// <summary>Vai trò (hệ thống tự động gán Requester, không cho phép client tự chọn).</summary>
    public string? Role { get; init; }
}
