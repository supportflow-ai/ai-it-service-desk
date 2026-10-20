using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using ServiceDesk.Application.Common.Interfaces;

namespace ServiceDesk.Infrastructure.Storage;

/// <summary>
/// Cài đặt dịch vụ lưu trữ đối tượng tương thích MinIO / S3.
/// </summary>
internal sealed class MinioObjectStorageService : IObjectStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinioObjectStorageService> _logger;

    public MinioObjectStorageService(IMinioClient minioClient, ILogger<MinioObjectStorageService> logger)
    {
        _minioClient = minioClient;
        _logger = logger;
    }

    /// <summary>Sinh pre-signed URL để client tải tệp tin lên bucket trực tiếp.</summary>
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

    /// <summary>Sinh pre-signed URL để client tải tệp tin xuống.</summary>
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

    /// <summary>Kiểm tra bucket có tồn tại hay không.</summary>
    public async Task<bool> BucketExistsAsync(
        string bucketName, CancellationToken cancellationToken = default)
    {
        var args = new BucketExistsArgs().WithBucket(bucketName);
        return await _minioClient.BucketExistsAsync(args, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Đảm bảo bucket tồn tại, tự động tạo mới nếu chưa có.</summary>
    public async Task EnsureBucketExistsAsync(
        string bucketName, CancellationToken cancellationToken = default)
    {
        if (!await BucketExistsAsync(bucketName, cancellationToken))
        {
            var args = new MakeBucketArgs().WithBucket(bucketName);
            await _minioClient.MakeBucketAsync(args, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Đã khởi tạo bucket MinIO: {BucketName}", bucketName);
        }
    }
}
