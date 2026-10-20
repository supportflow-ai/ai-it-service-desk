namespace ServiceDesk.Application.Identity.Contracts;

/// <summary>
/// Dữ liệu đầu vào yêu cầu đăng nhập hệ thống.
/// </summary>
public sealed record LoginRequest
{
    /// <summary>Địa chỉ email đã đăng ký.</summary>
    public required string Email { get; init; }

    /// <summary>Mật khẩu đăng nhập.</summary>
    public required string Password { get; init; }
}
