using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.Exceptions;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Infrastructure.Options;

namespace ServiceDesk.Infrastructure.Storage;

/// <summary>
/// Cài đặt dịch vụ lưu trữ đối tượng tương thích MinIO / S3.
/// </summary>
internal sealed class MinioObjectStorageService : IObjectStorageService, IDisposable
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinioObjectStorageService> _logger;
    private readonly IMinioClient _signingClient;
    public string DefaultBucket { get; }

    public MinioObjectStorageService(IMinioClient minioClient, ILogger<MinioObjectStorageService> logger, IOptions<MinioOptions> configured)
    {
        _minioClient = minioClient;
        _logger = logger;
        var options = configured.Value;
        DefaultBucket = options.Bucket;
        _signingClient = minioClient;
        if (!string.IsNullOrWhiteSpace(options.PublicEndpoint))
        {
            if (!Uri.TryCreate(options.PublicEndpoint, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https") || uri.AbsolutePath != "/" ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.UserInfo))
                throw new ArgumentException("Minio:PublicEndpoint phải là HTTP(S) origin, không có path/query/credential.");
            _signingClient = new MinioClient().WithEndpoint(uri.Host, uri.Port)
                .WithCredentials(options.AccessKey, options.SecretKey).WithRegion(options.Region)
                .WithSSL(uri.Scheme == "https").Build();
        }
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

        return await _signingClient.PresignedPutObjectAsync(args).ConfigureAwait(false);
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

        return await _signingClient.PresignedGetObjectAsync(args).ConfigureAwait(false);
    }

    /// <summary>Lấy metadata object từ MinIO để xác thực xác nhận tải lên.</summary>
    public async Task<ObjectStorageObjectInfo> StatObjectAsync(
        string bucketName, string objectName, CancellationToken cancellationToken = default)
    {
        var args = new StatObjectArgs().WithBucket(bucketName).WithObject(objectName);
        var stat = await _minioClient.StatObjectAsync(args, cancellationToken).ConfigureAwait(false);
        return new ObjectStorageObjectInfo(stat.Size, stat.ContentType);
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

    public async Task<StoredObject?> StatAsync(string bucket, string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await _minioClient.StatObjectAsync(new StatObjectArgs().WithBucket(bucket).WithObject(key), cancellationToken);
            return new StoredObject(value.Size, value.ContentType, value.ETag);
        }
        catch (ObjectNotFoundException) { return null; }
    }

    public async Task<PresignedUpload> CreateUploadAsync(string bucket, string key, string contentType, long size, int expirySeconds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var policy = new PostPolicy();
        policy.SetBucket(bucket);
        policy.SetKey(key);
        policy.SetExpires(DateTime.UtcNow.AddSeconds(expirySeconds));
        policy.SetContentType(contentType);
        policy.SetContentRange(size, size);
        var (url, fields) = await _signingClient.PresignedPostPolicyAsync(policy);
        return new PresignedUpload(url.ToString(), new Dictionary<string, string>(fields));
    }

    public async Task CopyAsync(string bucket, string source, string destination, string expectedETag, CancellationToken cancellationToken = default)
    {
        var sourceArgs = new CopySourceObjectArgs().WithBucket(bucket).WithObject(source).WithMatchETag(expectedETag);
        await _minioClient.CopyObjectAsync(new CopyObjectArgs().WithBucket(bucket).WithObject(destination).WithCopyObjectSource(sourceArgs), cancellationToken);
    }

    public async Task DeleteAsync(string bucket, string key, CancellationToken cancellationToken = default)
    {
        try { await _minioClient.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucket).WithObject(key), cancellationToken); }
        catch (Exception)
        {
            _logger.LogWarning("Không thể dọn object tạm; cần thử lại qua tác vụ bảo trì.");
            throw;
        }
    }

    public void Dispose()
    {
        if (!ReferenceEquals(_signingClient, _minioClient)) _signingClient.Dispose();
    }
}
