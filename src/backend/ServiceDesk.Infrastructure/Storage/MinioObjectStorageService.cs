using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using ServiceDesk.Application.Common.Interfaces;

namespace ServiceDesk.Infrastructure.Storage;

/// <summary>
/// MinIO/S3-compatible object storage implementation.
/// Skeleton — actual file upload/download features are deferred to future sprints.
/// </summary>
public sealed class MinioObjectStorageService : IObjectStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinioObjectStorageService> _logger;

    public MinioObjectStorageService(IMinioClient minioClient, ILogger<MinioObjectStorageService> logger)
    {
        _minioClient = minioClient;
        _logger = logger;
    }

    public async Task<string> GetPresignedUploadUrlAsync(
        string bucketName, string objectName, int expiryInSeconds = 3600,
        CancellationToken cancellationToken = default)
    {
        var args = new PresignedPutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithExpiry(expiryInSeconds);

        return await _minioClient.PresignedPutObjectAsync(args).ConfigureAwait(false);
    }

    public async Task<string> GetPresignedDownloadUrlAsync(
        string bucketName, string objectName, int expiryInSeconds = 3600,
        CancellationToken cancellationToken = default)
    {
        var args = new PresignedGetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithExpiry(expiryInSeconds);

        return await _minioClient.PresignedGetObjectAsync(args).ConfigureAwait(false);
    }

    public async Task<bool> BucketExistsAsync(
        string bucketName, CancellationToken cancellationToken = default)
    {
        var args = new BucketExistsArgs().WithBucket(bucketName);
        return await _minioClient.BucketExistsAsync(args, cancellationToken).ConfigureAwait(false);
    }

    public async Task EnsureBucketExistsAsync(
        string bucketName, CancellationToken cancellationToken = default)
    {
        if (!await BucketExistsAsync(bucketName, cancellationToken))
        {
            var args = new MakeBucketArgs().WithBucket(bucketName);
            await _minioClient.MakeBucketAsync(args, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Created MinIO bucket: {BucketName}", bucketName);
        }
    }
}
