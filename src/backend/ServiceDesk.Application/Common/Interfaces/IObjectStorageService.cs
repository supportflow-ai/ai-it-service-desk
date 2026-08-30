namespace ServiceDesk.Application.Common.Interfaces;

/// <summary>
/// Application-owned abstraction over object/file storage (MinIO/S3-compatible).
/// Infrastructure provides the concrete implementation.
/// </summary>
public interface IObjectStorageService
{
    /// <summary>
    /// Generates a pre-signed URL for uploading an object.
    /// </summary>
    Task<string> GetPresignedUploadUrlAsync(
        string bucketName,
        string objectName,
        int expiryInSeconds = 3600,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a pre-signed URL for downloading an object.
    /// </summary>
    Task<string> GetPresignedDownloadUrlAsync(
        string bucketName,
        string objectName,
        int expiryInSeconds = 3600,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a bucket exists.
    /// </summary>
    Task<bool> BucketExistsAsync(
        string bucketName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the bucket exists, creating it if necessary.
    /// </summary>
    Task EnsureBucketExistsAsync(
        string bucketName,
        CancellationToken cancellationToken = default);
}
