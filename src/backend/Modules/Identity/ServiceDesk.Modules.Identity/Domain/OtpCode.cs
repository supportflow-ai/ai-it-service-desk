namespace ServiceDesk.Modules.Identity.Domain;

/// <summary>
/// Đại diện cho mã xác thực một lần (OTP 6 số cho Reset Password, Verify Email).
/// Mỗi mã chỉ cho phép <see cref="MaxFailedAttempts"/> lần nhập sai; sau đó mã không còn được chấp nhận.
/// </summary>
public sealed class OtpCode
{
    /// <summary>Mục đích của mã đặt lại mật khẩu.</summary>
    public const string ResetPasswordPurpose = "ResetPassword";

    /// <summary>Số lần nhập sai tối đa cho một mã.</summary>
    public const int MaxFailedAttempts = 5;

    /// <summary>Số mã tối đa được phát cho một email trong một giờ.</summary>
    public const int MaxIssuedPerHour = 5;

    /// <summary>Thời gian hiệu lực của một mã.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    public Guid Id { get; set; }
    public string Target { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsUsed { get; set; }

    /// <summary>Số lần đã nhập sai mã này.</summary>
    public int FailedAttempts { get; set; }

    public bool IsValid(DateTime now) => !IsUsed && now <= ExpiresAt && FailedAttempts < MaxFailedAttempts;

    /// <summary>Tạo mã mới cho một đích (email đã chuẩn hóa) và mục đích.</summary>
    public static OtpCode Issue(string target, string purpose, string codeHash, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        Target = target,
        CodeHash = codeHash,
        Purpose = purpose,
        CreatedAt = now,
        ExpiresAt = now.Add(Lifetime),
        IsUsed = false,
        FailedAttempts = 0
    };
}
