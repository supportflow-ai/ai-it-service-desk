using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace ServiceDesk.Modules.Identity.Infrastructure.Options;

/// <summary>Kiểm tra cấu hình SMTP lúc khởi động khi kênh gửi đã được bật.</summary>
internal sealed class SmtpEmailOptionsValidator : IValidateOptions<SmtpEmailOptions>
{
    public ValidateOptionsResult Validate(string? name, SmtpEmailOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.IsConfigured) return ValidateOptionsResult.Success;

        var errors = new List<string>();
        if (options.Port is <= 0 or > 65535) errors.Add("Email:Smtp:Port phải nằm trong khoảng 1–65535.");
        if (!MailAddress.TryCreate(options.FromAddress, out _)) errors.Add("Email:Smtp:FromAddress phải là địa chỉ email hợp lệ.");
        if (!string.IsNullOrEmpty(options.UserName) && string.IsNullOrEmpty(options.Password))
            errors.Add("Email:Smtp:Password là bắt buộc khi có Email:Smtp:UserName.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
