namespace ServiceDesk.Domain.Identity;

/// <summary>
/// Đại diện cho token làm mới phiên đăng nhập (Refresh Token Rotation & Revocation).
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? DeviceInfo { get; set; }
    public string? IpAddress { get; set; }

    public bool IsActive => RevokedAt == null && DateTime.UtcNow < ExpiresAt;
}
