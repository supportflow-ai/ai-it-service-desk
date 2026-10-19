using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ServiceDesk.Infrastructure.Options;

public sealed class JwtOptionsValidator(IHostEnvironment environment) : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (Encoding.UTF8.GetByteCount(options.SecretKey ?? "") < 32 ||
            string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience) || options.ExpiryMinutes <= 0)
            return ValidateOptionsResult.Fail("JWT requires a key of at least 32 UTF-8 bytes, issuer, audience and positive lifetime.");
        if (environment.IsProduction() && (options.SecretKey!.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase) ||
                                          options.SecretKey.Contains("development", StringComparison.OrdinalIgnoreCase)))
            return ValidateOptionsResult.Fail("Production JWT signing key must be supplied securely and must not use sample values.");
        return ValidateOptionsResult.Success;
    }
}
