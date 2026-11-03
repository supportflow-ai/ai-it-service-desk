using System.Net.Http.Headers;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Minio;
using ServiceDesk.Infrastructure.Options;
using ServiceDesk.Infrastructure.Storage;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class MinioAttachmentUploadTests
{
    [Fact]
    public async Task SignedPolicyEnforcesSizeAndCopyIsNotWritableByOldUploadForm()
    {
        await using var container = new ContainerBuilder("bitnamilegacy/minio:2025.7.23-debian-12-r3")
            .WithEnvironment("MINIO_ROOT_USER", "synthetic-user")
            .WithEnvironment("MINIO_ROOT_PASSWORD", "synthetic-only-password")
            .WithPortBinding(9000, true).WithCommand("server", "/data")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
                request.ForPort(9000).ForPath("/minio/health/ready"))).Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await container.StartAsync(timeout.Token);
        var port = container.GetMappedPublicPort(9000);
        var options = new MinioOptions
        {
            Endpoint = $"{container.Hostname}:{port}", PublicEndpoint = $"http://{container.Hostname}:{port}",
            Bucket = "attachment-policy-tests", AccessKey = "synthetic-user", SecretKey = "synthetic-only-password"
        };
        using var client = new MinioClient().WithEndpoint(container.Hostname, port)
            .WithCredentials(options.AccessKey, options.SecretKey).Build();
        using var adapter = new MinioObjectStorageService(client, NullLogger<MinioObjectStorageService>.Instance, Options.Create(options));
        await adapter.EnsureBucketExistsAsync(options.Bucket, timeout.Token);
        var upload = await adapter.CreateUploadAsync(options.Bucket, "uploads/pending/test-object", "text/plain", 5, 60, timeout.Token);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        using (var exact = await Post(http, upload, "hello", timeout.Token)) Assert.True(exact.IsSuccessStatusCode);
        var stored = await adapter.StatAsync(options.Bucket, "uploads/pending/test-object", timeout.Token);
        Assert.NotNull(stored);
        Assert.Equal(5, stored.Size);
        await adapter.CopyAsync(options.Bucket, "uploads/pending/test-object", "tickets/test/attachments/final", stored.ETag, timeout.Token);
        using (var wrongSize = await Post(http, upload, "too long", timeout.Token)) Assert.False(wrongSize.IsSuccessStatusCode);
        using (var replay = await Post(http, upload, "other", timeout.Token)) Assert.True(replay.IsSuccessStatusCode);
        using var downloaded = await http.GetAsync(await adapter.GetPresignedDownloadUrlAsync(options.Bucket, "tickets/test/attachments/final", 60, timeout.Token), timeout.Token);
        Assert.Equal("hello", await downloaded.Content.ReadAsStringAsync(timeout.Token));
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client,
        ServiceDesk.Application.Common.Interfaces.PresignedUpload upload, string value, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        foreach (var field in upload.FormData) form.Add(new StringContent(field.Value), field.Key);
        var file = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(value));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "example.txt");
        return await client.PostAsync(upload.Url, form, cancellationToken);
    }
}
