namespace ServiceDesk.Infrastructure.Options;

/// <summary>
/// Các tùy chọn cấu hình cho cơ chế xác thực JWT Bearer Token.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Khóa bí mật ký token (tối thiểu 32 bytes UTF-8).</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Đơn vị phát hành token (Issuer).</summary>
    public string Issuer { get; set; } = "ServiceDesk";

    /// <summary>Đối tượng tiếp nhận token hợp lệ (Audience).</summary>
    public string Audience { get; set; } = "ServiceDeskClient";

    /// <summary>Thời gian sống của token tính bằng phút.</summary>
    public int ExpiryMinutes { get; set; } = 60;
}
