using System.Security.Cryptography;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Identity.Common;

/// <summary>Tạo refresh token ngẫu nhiên; chỉ giá trị băm được lưu, giá trị gốc trả cho client một lần.</summary>
internal static class RefreshTokenIssuer
{
    /// <summary>Thời gian sống của refresh token.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public static (RefreshToken Entity, string RawToken) Issue(string userId, DateTime now, string? deviceInfo = null, string? ipAddress = null)
    {
        var raw = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(64));
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = TokenHasher.Hash(raw),
            CreatedAt = now,
            ExpiresAt = now.Add(Lifetime),
            DeviceInfo = deviceInfo,
            IpAddress = ipAddress
        };
        return (entity, raw);
    }
}
