namespace ServiceDesk.Modules.Identity.Infrastructure.Options;

public sealed class OtpProtectionOptions
{
    public const string SectionName = "Otp";
    public string Key { get; set; } = string.Empty;

    internal bool HasValidKey()
    {
        Span<byte> bytes = stackalloc byte[32];
        return Key.Length == 44 && Convert.TryFromBase64String(Key, bytes, out var written) && written == 32;
    }
}
