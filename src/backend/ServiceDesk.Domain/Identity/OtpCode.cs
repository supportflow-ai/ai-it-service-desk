namespace ServiceDesk.Domain.Identity;

/// <summary>
/// Đại diện cho mã xác thực một lần (OTP 6 số cho Reset Password, Verify Email).
/// </summary>
public sealed class OtpCode
{
    public Guid Id { get; set; }
    public string Target { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsUsed { get; set; }

    public bool IsValid(DateTime now) => !IsUsed && now <= ExpiresAt;
}
