namespace ServiceDesk.Platform.Options;

/// <summary>
/// Tùy chọn cấu hình dịch vụ lưu trữ Cloudflare R2 tương thích S3 (ADR-017).
/// </summary>
public sealed class R2Options
{
    public const string SectionName = "R2";

    /// <summary>Điểm cuối S3 của Cloudflare R2 (ví dụ: https://&lt;ACCOUNT_ID&gt;.r2.cloudflarestorage.com).</summary>
    public string Endpoint { get; set; } = string.Empty;

    public string AccessKeyId { get; set; } = string.Empty;

    public string SecretAccessKey { get; set; } = string.Empty;

    public string Bucket { get; set; } = string.Empty;

    public int UploadExpirySeconds { get; set; } = 900;

    public int DownloadExpirySeconds { get; set; } = 300;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) &&
        !string.IsNullOrWhiteSpace(AccessKeyId) &&
        !string.IsNullOrWhiteSpace(SecretAccessKey) &&
        !string.IsNullOrWhiteSpace(Bucket);
}
