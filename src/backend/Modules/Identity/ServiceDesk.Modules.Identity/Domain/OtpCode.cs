using System.Security.Cryptography;
using System.Text;

namespace ServiceDesk.Modules.Identity.Domain;

/// <summary>
/// Mã xác thực một lần (OTP 6 số) cho đặt lại mật khẩu. Chỉ lưu giá trị băm.
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

    private OtpCode()
    {
    }

    public Guid Id { get; private set; }
    public string Target { get; private set; } = string.Empty;
    public string CodeHash { get; private set; } = string.Empty;
    public string Purpose { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsUsed { get; private set; }

    /// <summary>Số lần đã nhập sai mã này.</summary>
    public int FailedAttempts { get; private set; }

    /// <summary>Mã còn được chấp nhận: chưa dùng, chưa hết hạn và chưa vượt số lần nhập sai.</summary>
    public bool IsValid(DateTime now) => !IsUsed && now <= ExpiresAt && FailedAttempts < MaxFailedAttempts;

    /// <summary>So sánh giá trị băm của mã người dùng nhập với mã đã lưu, thời gian so sánh không phụ thuộc nội dung.</summary>
    public bool Matches(string codeHash)
    {
        ArgumentNullException.ThrowIfNull(codeHash);
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(CodeHash), Encoding.ASCII.GetBytes(codeHash));
    }

    /// <summary>Tạo mã mới cho một đích (email đã chuẩn hóa) và mục đích.</summary>
    public static OtpCode Issue(string target, string purpose, string codeHash, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeHash);
        return new OtpCode
        {
            Id = Guid.NewGuid(),
            Target = target,
            CodeHash = codeHash,
            Purpose = purpose,
            CreatedAt = now,
            ExpiresAt = now.Add(Lifetime)
        };
    }
}
