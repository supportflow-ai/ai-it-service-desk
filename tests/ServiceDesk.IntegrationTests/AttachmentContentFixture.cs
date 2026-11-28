using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Amazon.S3;
using Amazon.S3.Model;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[CollectionDefinition("Attachment scanning resource", DisableParallelization = true)]
public sealed class AttachmentContentCollection;

public sealed class AttachmentContentFixture : IAsyncLifetime
{
    internal const string TestAccess = "attachment-test";
    internal const string TestSecret = "local-disposable-attachment-test-only";
    internal const string Bucket = "attachment-private";
    private readonly IContainer _s3 = new ContainerBuilder("bitnamilegacy/minio@sha256:953d489a81cc4de7975f90e07202189c4325da39b0b92470b6a13c7ea99e36cd")
        .WithEntrypoint("/opt/bitnami/minio/bin/minio").WithCommand("server", "/tmp/attachment-data")
        .WithEnvironment("MINIO_ROOT_USER", TestAccess).WithEnvironment("MINIO_ROOT_PASSWORD", TestSecret)
        .WithPortBinding(9000, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(9000).ForPath("/minio/health/ready")))
        .Build();
    private IContainer? _clam;
    public string Endpoint => $"http://{_s3.Hostname}:{_s3.GetMappedPublicPort(9000)}";
    public string ScannerHost { get; private set; } = string.Empty;
    public int ScannerPort { get; private set; }
    public AmazonS3Client Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _s3.StartAsync();
        Client = new AmazonS3Client(TestAccess, TestSecret, new AmazonS3Config { ServiceURL = Endpoint, AuthenticationRegion = "auto", ForcePathStyle = true });
        await Client.PutBucketAsync(new PutBucketRequest { BucketName = Bucket });
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../config/clamav/clamd.conf"));
        _clam = new ContainerBuilder("clamav/clamav@sha256:64e30c0bf976198024deb3dc6d5cfe2485fb969c0f73549ba0fdb364b9469614")
            .WithEnvironment("CLAMAV_NO_FRESHCLAMD", "true")
            .WithResourceMapping(File.ReadAllBytes(path), "/etc/clamav/clamd.conf")
            .WithPortBinding(3310, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(3310, wait => wait.WithTimeout(TimeSpan.FromMinutes(3))))
            .Build();
        await _clam.StartAsync();
        ScannerHost = _clam.Hostname; ScannerPort = _clam.GetMappedPublicPort(3310);
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_clam is not null) await _clam.DisposeAsync();
        await _s3.DisposeAsync();
    }
}
