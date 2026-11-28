using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
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
    private byte[] DeliveryKey() => HKDF.DeriveKey(HashAlgorithmName.SHA256, key, 32, null, "SupportFlow.OTP.delivery.v1"u8.ToArray());
    private static byte[] Context(string email, Guid otpId) => JsonSerializer.SerializeToUtf8Bytes(new[] { "SupportFlow.OTP.delivery.v1", email, otpId.ToString("N") });

    public string Protect(string normalizedEmail, Guid otpId, string code)
    {
        var plain = Encoding.UTF8.GetBytes(code);
        var payload = new byte[28 + plain.Length];
        RandomNumberGenerator.Fill(payload.AsSpan(0, 12));
        using var aes = new AesGcm(DeliveryKey(), 16);
        aes.Encrypt(payload.AsSpan(0, 12), plain, payload.AsSpan(28), payload.AsSpan(12, 16), Context(normalizedEmail, otpId));
        return "v1:" + Convert.ToBase64String(payload);
    }

    public string Unprotect(string normalizedEmail, Guid otpId, string protectedCode)
    {
        if (!protectedCode.StartsWith("v1:", StringComparison.Ordinal) || protectedCode.Length > 1024)
            throw new CryptographicException("Payload OTP không hợp lệ.");
        byte[] payload;
        try { payload = Convert.FromBase64String(protectedCode[3..]); }
        catch (FormatException) { throw new CryptographicException("Payload OTP không hợp lệ."); }
        if (payload.Length != 34) throw new CryptographicException("Payload OTP không hợp lệ.");
        var plain = new byte[6];
        using var aes = new AesGcm(DeliveryKey(), 16);
        aes.Decrypt(payload.AsSpan(0, 12), payload.AsSpan(28), payload.AsSpan(12, 16), plain, Context(normalizedEmail, otpId));
        if (plain.Any(b => b < '0' || b > '9')) throw new CryptographicException("Payload OTP không hợp lệ.");
        return Encoding.UTF8.GetString(plain);
    }
}
