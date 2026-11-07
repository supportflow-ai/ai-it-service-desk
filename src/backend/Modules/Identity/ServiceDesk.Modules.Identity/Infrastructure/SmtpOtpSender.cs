using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Infrastructure.Options;

namespace ServiceDesk.Modules.Identity.Infrastructure;

/// <summary>
/// Gửi mã OTP đặt lại mật khẩu qua SMTP. Lỗi gửi chỉ được ghi log (không ghi mã, không ghi email) và không ném ra,
/// để phản hồi của "quên mật khẩu" giống nhau dù email có tồn tại hay không.
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
            logger.LogError(ex, "Gửi mã OTP đặt lại mật khẩu qua SMTP thất bại.");
        }
    }
}
