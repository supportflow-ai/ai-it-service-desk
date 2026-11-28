namespace ServiceDesk.Modules.Identity.Domain;

/// <summary>Yêu cầu phát/gửi OTP bền vững; payload chỉ chứa ciphertext.</summary>
public sealed class PasswordResetJob
{
    public const int Capacity = 1000;
    public const int MaxAttempts = 5;
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime NextAttemptAt { get; private set; }
    public int Attempts { get; private set; }
    public Guid? LeaseId { get; private set; }
    public DateTime? LeaseUntil { get; private set; }
    public Guid? OtpCodeId { get; private set; }
    public string? ProtectedCode { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? FailureCode { get; private set; }

    public static PasswordResetJob Request(string email, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        if (email.Length > 320) throw new ArgumentException("Email vượt quá giới hạn cho phép.", nameof(email));
        return new PasswordResetJob { Id = Guid.NewGuid(), Email = email, CreatedAt = now, NextAttemptAt = now, ExpiresAt = now.Add(OtpCode.Lifetime) };
    }

    public void Prepare(OtpCode code, string protectedCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedCode);
        if (code.Target != Email || code.Purpose != OtpCode.ResetPasswordPurpose)
            throw new ArgumentException("Mã không thuộc yêu cầu đặt lại mật khẩu.", nameof(code));
        OtpCodeId = code.Id;
        ProtectedCode = protectedCode;
        ExpiresAt = code.ExpiresAt;
    }
}
