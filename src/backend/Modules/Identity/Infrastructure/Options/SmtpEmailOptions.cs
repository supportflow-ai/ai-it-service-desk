namespace ServiceDesk.Modules.Identity.Infrastructure.Options;

/// <summary>
/// Máy chủ SMTP dùng để gửi mã OTP đặt lại mật khẩu. Để trống <see cref="Host"/> thì không gửi
/// (dùng <c>NoOpOtpSender</c>). Mật khẩu chỉ cấp qua biến môi trường hoặc kho bí mật, không ghi vào appsettings.
/// </summary>
public sealed class SmtpEmailOptions
{
    public const string SectionName = "Email:Smtp";

    /// <summary>Tên máy chủ SMTP; để trống nghĩa là chưa cấu hình kênh gửi.</summary>
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    /// <summary>Bật STARTTLS/SSL; chỉ tắt cho máy chủ thử nghiệm cục bộ.</summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>Tài khoản đăng nhập SMTP; để trống nếu máy chủ không yêu cầu xác thực.</summary>
    public string? UserName { get; set; }

    public string? Password { get; set; }

    /// <summary>Địa chỉ người gửi, bắt buộc khi đã có <see cref="Host"/>.</summary>
    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "SupportFlow";

    /// <summary>Đã cấu hình kênh gửi hay chưa.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}
