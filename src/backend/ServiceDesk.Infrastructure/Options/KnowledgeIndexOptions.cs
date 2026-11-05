namespace ServiceDesk.Infrastructure.Options;

public sealed class KnowledgeIndexOptions
{
    public const string SectionName = "KnowledgeIndex";
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "";
    public string BearerToken { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 60;
    public int LeaseSeconds { get; set; } = 180;
    public int MaxAttempts { get; set; } = 5;
    public int PollSeconds { get; set; } = 2;

    public bool IsValid() => TimeoutSeconds is >= 1 and <= 120 && LeaseSeconds > TimeoutSeconds + 30 &&
        LeaseSeconds <= 600 && MaxAttempts is >= 1 and <= 20 && PollSeconds is >= 1 and <= 60 &&
        (!Enabled || (Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https" && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.UserInfo) &&
        string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) &&
        BearerToken.Length >= 32 && !BearerToken.Any(char.IsWhiteSpace)));
}
