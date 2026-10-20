namespace ServiceDesk.Infrastructure.Options;

/// <summary>
/// Các tùy chọn cấu hình kết nối MinIO Object Storage (tương thích chuẩn AWS S3).
/// </summary>
public sealed class MinioOptions
{
    public const string SectionName = "Minio";

    /// <summary>Endpoint dịch vụ MinIO (ví dụ localhost:9000).</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Access Key tài khoản lưu trữ.</summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>Secret Key tài khoản lưu trữ.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Tên bucket mặc định để chứa tệp tin.</summary>
    public string Bucket { get; set; } = "servicedesk";

    /// <summary>Sử dụng kết nối bảo mật SSL (HTTPS) hay không.</summary>
    public bool UseSsl { get; set; }
}
