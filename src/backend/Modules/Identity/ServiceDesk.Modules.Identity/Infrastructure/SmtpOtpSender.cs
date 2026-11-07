using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Infrastructure.Options;

namespace ServiceDesk.Modules.Identity.Infrastructure;

/// <summary>
/// Gửi mã OTP đặt lại mật khẩu qua SMTP (chạy nền từ hàng đợi quên mật khẩu). Lỗi gửi chỉ ghi loại lỗi và mã trạng thái
/// SMTP; không ghi exception vì thông điệp của nó có thể chứa địa chỉ người nhận.
/// </summary>
internal sealed class SmtpOtpSender(IOptions<SmtpEmailOptions> options, ILogger<SmtpOtpSender> logger) : IOtpSender
{
    public async Task SendPasswordResetCodeAsync(string email, string code, DateTime expiresAt, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        using var message = PasswordResetEmail.Create(settings, email, code, expiresAt);
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        if (!string.IsNullOrEmpty(settings.UserName))
        {
            client.Credentials = new NetworkCredential(settings.UserName, settings.Password);
        }

        try
        {
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or IOException)
        {
            logger.LogError("Gửi mã OTP đặt lại mật khẩu qua SMTP thất bại ({ErrorType}, {SmtpStatus}).",
                ex.GetType().Name, (ex as SmtpException)?.StatusCode);
        }
    }
}
