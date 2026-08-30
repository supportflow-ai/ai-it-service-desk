namespace ServiceDesk.Infrastructure.Options;

/// <summary>
/// Strongly typed JWT authentication options.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "ServiceDesk";
    public string Audience { get; set; } = "ServiceDeskClient";
    public int ExpiryMinutes { get; set; } = 60;
}
