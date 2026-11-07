using System.Security.Cryptography;
using ServiceDesk.Modules.Identity.Domain;

namespace ServiceDesk.Modules.Identity.Application.Common;

/// <summary>Tạo refresh token ngẫu nhiên; chỉ giá trị băm được lưu, giá trị gốc trả cho client một lần.</summary>
internal static class RefreshTokenIssuer
{
    public static (RefreshToken Entity, string RawToken) Issue(string userId, DateTime now, string? deviceInfo = null, string? ipAddress = null)
    {
        var raw = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(64));
        return (RefreshToken.Issue(userId, TokenHasher.Hash(raw), now, deviceInfo, ipAddress), raw);
    }
}
