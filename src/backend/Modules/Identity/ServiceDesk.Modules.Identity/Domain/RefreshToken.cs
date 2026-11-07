namespace ServiceDesk.Modules.Identity.Domain;

/// <summary>
/// Token làm mới phiên đăng nhập. Chỉ lưu giá trị băm; giá trị gốc chỉ trả cho client một lần.
/// Token được thu hồi khi xoay vòng, đăng xuất, đổi/đặt lại mật khẩu hoặc đổi vai trò.
/// </summary>
public sealed class RefreshToken
{
    /// <summary>Thời gian sống của refresh token.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private RefreshToken()
    {
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? DeviceInfo { get; private set; }
    public string? IpAddress { get; private set; }

    /// <summary>Token đã bị thu hồi (dùng lại token này được coi là dấu hiệu bị lộ).</summary>
    public bool IsRevoked => RevokedAt is not null;

    /// <summary>Token đã hết hạn tại thời điểm <paramref name="now"/>.</summary>
    public bool IsExpiredAt(DateTime now) => ExpiresAt <= now;

    /// <summary>Token còn dùng được: chưa thu hồi và chưa hết hạn.</summary>
    public bool IsActiveAt(DateTime now) => !IsRevoked && !IsExpiredAt(now);

    /// <summary>Tạo token mới cho người dùng, hết hạn sau <see cref="Lifetime"/>.</summary>
    public static RefreshToken Issue(string userId, string tokenHash, DateTime now, string? deviceInfo = null, string? ipAddress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now.Add(Lifetime),
            DeviceInfo = deviceInfo,
            IpAddress = ipAddress
        };
    }

    /// <summary>Thu hồi token; gọi lại trên token đã thu hồi không đổi thời điểm thu hồi ban đầu.</summary>
    public void Revoke(DateTime now) => RevokedAt ??= now;
}
