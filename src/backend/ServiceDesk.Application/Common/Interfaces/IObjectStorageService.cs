namespace ServiceDesk.Application.Common.Interfaces;

public sealed record StoredObject(long Size, string ContentType, string ETag);
public sealed record PresignedUpload(string Url, IReadOnlyDictionary<string, string> FormData);

/// <summary>
/// Cổng trừu tượng hóa dịch vụ lưu trữ đối tượng/tệp tin (MinIO/S3-compatible) do tầng Application làm chủ.
/// Tầng Infrastructure cung cấp cài đặt cụ thể.
/// </summary>
public interface IObjectStorageService
{
    string DefaultBucket { get; }
    Task<PresignedUpload> CreateUploadAsync(string bucket, string key, string contentType, long size, int expirySeconds, CancellationToken cancellationToken = default);
    Task<StoredObject?> StatAsync(string bucket, string key, CancellationToken cancellationToken = default);
    Task CopyAsync(string bucket, string source, string destination, string expectedETag, CancellationToken cancellationToken = default);
    Task DeleteAsync(string bucket, string key, CancellationToken cancellationToken = default);
    /// <summary>
    /// Tạo URL ký sẵn (Pre-signed URL) để tải tệp tin lên lưu trữ.
    /// </summary>
    Task<string> GetPresignedUploadUrlAsync(
        string bucketName,
        string objectName,
        int expiryInSeconds = 3600,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo URL ký sẵn (Pre-signed URL) để tải tệp tin xuống.
    /// </summary>
    Task<string> GetPresignedDownloadUrlAsync(
        string bucketName,
        string objectName,
        int expiryInSeconds = 3600,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy metadata của object đã tải lên mà không tin dữ liệu do client gửi.
    /// </summary>
    Task<ObjectStorageObjectInfo> StatObjectAsync(
        string bucketName,
        string objectName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra xem bucket có tồn tại hay không.
    /// </summary>
    Task<bool> BucketExistsAsync(
        string bucketName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đảm bảo bucket đã tồn tại, tự động tạo mới nếu chưa có.
    /// </summary>
    Task EnsureBucketExistsAsync(
        string bucketName,
        CancellationToken cancellationToken = default);
}
