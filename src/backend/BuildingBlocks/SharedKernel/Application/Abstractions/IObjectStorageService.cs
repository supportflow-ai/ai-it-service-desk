namespace ServiceDesk.SharedKernel.Application.Abstractions;

public sealed record StoredObject(long Size, string ContentType, string ETag);
public sealed record PresignedUpload(
    string Url,
    IReadOnlyDictionary<string, string>? FormData = null,
    IReadOnlyDictionary<string, string>? RequiredHeaders = null);

/// <summary>
/// Cổng trừu tượng hóa dịch vụ lưu trữ đối tượng/tệp tin (S3/Cloudflare R2-compatible) do tầng Application làm chủ.
/// Tầng Platform cung cấp cài đặt cụ thể.
/// </summary>
public interface IObjectStorageService
{
    string DefaultBucket { get; }

    Task<PresignedUpload> CreateUploadAsync(
        string bucket,
        string key,
        string contentType,
        long size,
        int expirySeconds,
        CancellationToken cancellationToken = default);

    Task<StoredObject?> StatAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default);

    Task CopyAsync(
        string bucket,
        string source,
        string destination,
        string expectedETag,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default);

    Task<string> GetPresignedDownloadUrlAsync(
        string bucketName,
        string objectName,
        int expiryInSeconds = 3600,
        CancellationToken cancellationToken = default);
}
