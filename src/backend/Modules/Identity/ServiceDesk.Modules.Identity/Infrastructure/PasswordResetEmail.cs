using System.Globalization;
using System.Net.Mail;
using System.Text;
using ServiceDesk.Modules.Identity.Infrastructure.Options;

namespace ServiceDesk.Modules.Identity.Infrastructure;

/// <summary>Soạn email chứa mã OTP đặt lại mật khẩu.</summary>
internal static class PasswordResetEmail
{
    public const string Subject = "Mã đặt lại mật khẩu SupportFlow";

    public static MailMessage Create(SmtpEmailOptions options, string recipient, string code, DateTime expiresAt)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var expiry = expiresAt.ToUniversalTime().ToString("HH:mm 'UTC' dd/MM/yyyy", CultureInfo.InvariantCulture);
        var body = new StringBuilder()
            .AppendLine("Xin chào,")
            .AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"Mã đặt lại mật khẩu của bạn là: {code}")
            .AppendLine(CultureInfo.InvariantCulture, $"Mã hết hạn lúc {expiry} và chỉ dùng được một lần.")
            .AppendLine()
            .AppendLine("Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này.")
            .ToString();

        var message = new MailMessage
        {
            From = new MailAddress(options.FromAddress, options.FromName, Encoding.UTF8),
            Subject = Subject,
            SubjectEncoding = Encoding.UTF8,
            Body = body,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(recipient));
        return message;
    }
}
