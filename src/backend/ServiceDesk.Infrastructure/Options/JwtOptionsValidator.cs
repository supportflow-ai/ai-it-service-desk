using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ServiceDesk.Infrastructure.Options;

/// <summary>
/// Trình xác thực cấu hình JwtOptions lúc khởi chạy:
/// - Khóa SecretKey tối thiểu 32 bytes UTF-8.
/// - Không để trống Issuer, Audience và ExpiryMinutes > 0.
/// - Chặn việc dùng khóa mặc định CHANGE_ME / development trên môi trường Production.
/// </summary>
public sealed class JwtOptionsValidator(IHostEnvironment environment) : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (Encoding.UTF8.GetByteCount(options.SecretKey ?? "") < 32 ||
            string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience) || options.ExpiryMinutes <= 0)
            return ValidateOptionsResult.Fail("JWT yêu cầu khóa ký tối thiểu 32 bytes UTF-8, issuer, audience và thời hạn dương.");
        if (environment.IsProduction() && (options.SecretKey!.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase) ||
                                          options.SecretKey.Contains("development", StringComparison.OrdinalIgnoreCase)))
            return ValidateOptionsResult.Fail("Khóa ký JWT trên môi trường Production phải được cung cấp an toàn, không dùng giá trị mẫu.");
        return ValidateOptionsResult.Success;
    }
}
