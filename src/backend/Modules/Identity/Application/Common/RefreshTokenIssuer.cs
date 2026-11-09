using System.Security.Cryptography;
using ServiceDesk.Modules.Identity.Domain;

namespace ServiceDesk.Modules.Identity.Application.Common;

/// <summary>Tạo refresh token ngẫu nhiên; chỉ giá trị băm được lưu, giá trị gốc trả cho client một lần.</summary>
internal static class RefreshTokenIssuer
{
    /// <summary>Token đầu chuỗi phiên, phát khi đăng nhập.</summary>
    public static (RefreshToken Entity, string RawToken) Issue(string userId, DateTime now, string? deviceInfo = null, string? ipAddress = null)
    {
        var raw = NewRawToken();
        return (RefreshToken.Issue(userId, TokenHasher.Hash(raw), now, deviceInfo, ipAddress), raw);
    }

    /// <summary>Token thay thế khi xoay vòng, cùng chuỗi phiên với <paramref name="current"/>.</summary>
    public static (RefreshToken Entity, string RawToken) Rotate(RefreshToken current, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(current);
        var raw = NewRawToken();
        return (current.Rotate(TokenHasher.Hash(raw), now), raw);
    }

    private static string NewRawToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(64));
}
