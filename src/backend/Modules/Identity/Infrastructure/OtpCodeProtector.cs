using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Infrastructure.Options;

namespace ServiceDesk.Modules.Identity.Infrastructure;

internal sealed class OtpCodeProtector : IOtpCodeProtector
{
    private readonly byte[] key;
    public OtpCodeProtector(IOptions<OtpProtectionOptions> options)
    {
        if (!options.Value.HasValidKey())
            throw new OptionsValidationException(OtpProtectionOptions.SectionName, typeof(OtpProtectionOptions), ["Otp:Key cần khóa riêng 32 byte được mã hóa Base64."]);
        key = Convert.FromBase64String(options.Value.Key);
    }

    public string Hash(string normalizedEmail, string purpose, string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var input = JsonSerializer.SerializeToUtf8Bytes(new[] { "SupportFlow.OTP.v1", purpose, normalizedEmail, code });
        return "v1:" + Convert.ToHexStringLower(HMACSHA256.HashData(key, input));
    }
}
