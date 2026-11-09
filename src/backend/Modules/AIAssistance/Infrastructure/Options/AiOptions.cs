namespace ServiceDesk.Modules.AIAssistance.Infrastructure.Options;

/// <summary>Cấu hình kết nối S2S; mặc định giữ NoOp cho đến khi chủ động bật.</summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string BearerToken { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 5;

    public bool IsValid() => !Enabled ||
        (Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) &&
         uri.Scheme is "http" or "https" && uri.AbsolutePath == "/" &&
         string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) &&
         string.IsNullOrEmpty(uri.Fragment) && BearerToken.Length >= 32 &&
         !BearerToken.Any(char.IsWhiteSpace) && TimeoutSeconds is >= 1 and <= 30);
}
