namespace ServiceDesk.Application.Identity.Ports;

/// <summary>Cổng gửi mã OTP tới người dùng (email hoặc kênh khác do hạ tầng chọn).</summary>
public interface IOtpSender
{
    Task SendPasswordResetCodeAsync(string email, string code, DateTime expiresAt, CancellationToken cancellationToken = default);
}
