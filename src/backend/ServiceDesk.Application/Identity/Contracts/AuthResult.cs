namespace ServiceDesk.Application.Identity.Contracts;

/// <summary>
/// Kết quả trả về của các thao tác xác thực (đăng ký / đăng nhập).
/// </summary>
public sealed record AuthResult
{
    /// <summary>Cho biết thao tác xác thực thành công hay thất bại.</summary>
    public bool Succeeded { get; init; }

    /// <summary>Mã truy cập JWT Bearer Token khi thành công.</summary>
    public string? Token { get; init; }

    /// <summary>Danh sách lỗi mô tả nếu thao tác thất bại.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>Thông tin người dùng sau khi xác thực thành công.</summary>
    public UserInfo? User { get; init; }

    public static AuthResult Success(string token, UserInfo user) => new()
    {
        Succeeded = true,
        Token = token,
        User = user
    };

    public static AuthResult Failed(params string[] errors) => new()
    {
        Succeeded = false,
        Errors = errors
    };

    public static AuthResult Failed(IEnumerable<string> errors) => new()
    {
        Succeeded = false,
        Errors = errors.ToList()
    };
}
