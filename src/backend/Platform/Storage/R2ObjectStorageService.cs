using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceDesk.Platform.Options;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Platform.Storage;

/// <summary>
/// Cài đặt dịch vụ lưu trữ đối tượng Cloudflare R2 qua AWS SDK S3 (ADR-017).
/// </summary>
internal sealed class R2ObjectStorageService : IObjectStorageService, IDisposable
{
    private readonly IAmazonS3? _s3Client;
    private readonly ILogger<R2ObjectStorageService> _logger;
    private readonly R2Options _options;

    public string DefaultBucket => _options.Bucket;

    public R2ObjectStorageService(IOptions<R2Options> options, ILogger<R2ObjectStorageService> logger)
    {
        _logger = logger;
        _options = options.Value;

        if (_options.IsConfigured)
        {
            var config = new AmazonS3Config
            {
                ServiceURL = _options.Endpoint,
                AuthenticationRegion = "auto",
                ForcePathStyle = true
            };
            _s3Client = new AmazonS3Client(_options.AccessKeyId, _options.SecretAccessKey, config);
        }
    }

    private IAmazonS3 EnsureClient() =>
        _s3Client ?? throw new ServiceUnavailableException("Dịch vụ lưu trữ tệp (Cloudflare R2) chưa được cấu hình.");

    public async Task<PresignedUpload> CreateUploadAsync(
        string bucket,
        string key,
        string contentType,
        long size,
        int expirySeconds,
        CancellationToken cancellationToken = default)
    {
        var client = EnsureClient();
        var effectiveExpiry = expirySeconds > 0 ? expirySeconds : _options.UploadExpirySeconds;

        var request = new GetPreSignedUrlRequest
        {
            BucketName = string.IsNullOrWhiteSpace(bucket) ? DefaultBucket : bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.AddSeconds(effectiveExpiry),
            ContentType = contentType
        };

        var url = await client.GetPreSignedURLAsync(request).ConfigureAwait(false);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = contentType
        };

        return new PresignedUpload(url, FormData: null, RequiredHeaders: headers);
    }

    public async Task<StoredObject?> StatAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default)
    {
        var client = EnsureClient();
        var targetBucket = string.IsNullOrWhiteSpace(bucket) ? DefaultBucket : bucket;

        try
        {
            var metadata = await client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = targetBucket,
                Key = key
            }, cancellationToken).ConfigureAwait(false);

            return new StoredObject(metadata.ContentLength, metadata.Headers.ContentType, metadata.ETag);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task CopyAsync(
        string bucket,
        string source,
        string destination,
        string expectedETag,
        CancellationToken cancellationToken = default)
    {
        var client = EnsureClient();
        var targetBucket = string.IsNullOrWhiteSpace(bucket) ? DefaultBucket : bucket;

        var request = new CopyObjectRequest
        {
            SourceBucket = targetBucket,
            SourceKey = source,
            DestinationBucket = targetBucket,
            DestinationKey = destination
        };

        if (!string.IsNullOrWhiteSpace(expectedETag))
        {
            request.ETagToMatch = expectedETag;
        }

        try
        {
            await client.CopyObjectAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            throw new ArgumentException("Bản sao tệp không khớp điều kiện ETag nguồn.", ex);
        }
    }

    public async Task DeleteAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default)
    {
        var client = EnsureClient();
        var targetBucket = string.IsNullOrWhiteSpace(bucket) ? DefaultBucket : bucket;

        try
        {
            await client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = targetBucket,
                Key = key
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi xóa object trong bucket {Bucket}", targetBucket);
            throw;
        }
    }

    public async Task<string> GetPresignedDownloadUrlAsync(
        string bucketName,
        string objectName,
        int expiryInSeconds = 3600,
        CancellationToken cancellationToken = default)
    {
        var client = EnsureClient();
        var effectiveExpiry = expiryInSeconds > 0 ? expiryInSeconds : _options.DownloadExpirySeconds;

        var request = new GetPreSignedUrlRequest
        {
            BucketName = string.IsNullOrWhiteSpace(bucketName) ? DefaultBucket : bucketName,
            Key = objectName,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddSeconds(effectiveExpiry)
        };

        return await client.GetPreSignedURLAsync(request).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _s3Client?.Dispose();
    }
}
