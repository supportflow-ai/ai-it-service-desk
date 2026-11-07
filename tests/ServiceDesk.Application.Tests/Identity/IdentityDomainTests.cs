using ServiceDesk.Modules.Identity.Domain;
using Xunit;

namespace ServiceDesk.Application.Tests.Identity;

/// <summary>
/// Hành vi của thực thể Identity. Đặt trong Application.Tests vì project này đã tham chiếu module Identity;
/// Domain.Tests chưa tham chiếu module này.
/// </summary>
public sealed class IdentityDomainTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IssuedRefreshTokenIsActiveUntilItsLifetimeEnds()
    {
        var token = RefreshToken.Issue("user-1", "hash", Now, "device", "203.0.113.10");

        Assert.Equal(Now.Add(RefreshToken.Lifetime), token.ExpiresAt);
        Assert.Equal("device", token.DeviceInfo);
        Assert.Equal("203.0.113.10", token.IpAddress);
        Assert.True(token.IsActiveAt(Now));
        Assert.False(token.IsActiveAt(token.ExpiresAt));
        Assert.True(token.IsExpiredAt(token.ExpiresAt));
    }

    [Fact]
    public void RevokingKeepsTheFirstRevocationTime()
    {
        var token = RefreshToken.Issue("user-1", "hash", Now);

        token.Revoke(Now.AddMinutes(1));
        token.Revoke(Now.AddMinutes(5));

        Assert.True(token.IsRevoked);
        Assert.Equal(Now.AddMinutes(1), token.RevokedAt);
        Assert.False(token.IsActiveAt(Now.AddMinutes(2)));
    }

    [Theory]
    [InlineData("", "hash")]
    [InlineData("user-1", " ")]
    public void RefreshTokenRequiresUserAndHash(string userId, string hash) =>
        Assert.ThrowsAny<ArgumentException>(() => RefreshToken.Issue(userId, hash, Now));

    [Fact]
    public void IssuedOtpIsValidForItsLifetimeOnly()
    {
        var otp = OtpCode.Issue("user@test.invalid", OtpCode.ResetPasswordPurpose, "abc123", Now);

        Assert.Equal(0, otp.FailedAttempts);
        Assert.False(otp.IsUsed);
        Assert.True(otp.IsValid(Now.Add(OtpCode.Lifetime)));
        Assert.False(otp.IsValid(Now.Add(OtpCode.Lifetime).AddTicks(1)));
    }

    [Fact]
    public void OtpMatchesOnlyTheSameHash()
    {
        var otp = OtpCode.Issue("user@test.invalid", OtpCode.ResetPasswordPurpose, "abc123", Now);

        Assert.True(otp.Matches("abc123"));
        Assert.False(otp.Matches("abc124"));
        Assert.False(otp.Matches("abc12"));
    }
}
