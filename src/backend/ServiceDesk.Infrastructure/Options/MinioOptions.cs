namespace ServiceDesk.Infrastructure.Options;

/// <summary>
/// Strongly typed MinIO connection options.
/// </summary>
public sealed class MinioOptions
{
    public const string SectionName = "Minio";

    public string Endpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = "servicedesk";
    public bool UseSsl { get; set; }
}
