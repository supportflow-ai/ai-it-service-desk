using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.Modules.Ticketing.Infrastructure;
using ServiceDesk.Platform.Options;
using ServiceDesk.Platform.Storage;
using ServiceDesk.SharedKernel.Application.Abstractions;
using Moq;
using ServiceDesk.Modules.Ticketing.Application.ConfirmAttachmentUpload;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Application.ReconcileAttachments;
using ServiceDesk.Platform.Clock;
using ServiceDesk.Platform.Persistence;
using ServiceDesk.SharedKernel.Application;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Collection("Attachment scanning resource")]
[Trait("Category", "Integration")]
public sealed class AttachmentContentIntegrationTests(AttachmentContentFixture content, TicketIdempotencyPostgresFixture database)
    : IClassFixture<AttachmentContentFixture>, IClassFixture<TicketIdempotencyPostgresFixture>
{
    [Theory]
    [InlineData("clean")]
    [InlineData("infected")]
    [InlineData("disguised")]
    public async Task RealStorageAndScannerControlConfirmationAndPrivateDownload(string kind)
    {
        var actor = Guid.NewGuid();
        var token = await database.CreateTokenAsync(actor);
        var ticket = Ticket.Create(actor, "Content scan", "Disposable scanner and storage", TicketCategories.NET, DateTime.UtcNow);
        await using (var db = database.CreateContext()) { db.Tickets.Add(ticket); await db.SaveChangesAsync(); }
        var bytes = kind == "infected" ? Eicar() : Encoding.UTF8.GetBytes("clean");
        var type = kind == "disguised" ? "image/png" : "text/plain";
        var name = kind == "disguised" ? "fake.png" : "evidence.txt";
        await using var host = database.First!.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.PostConfigure<R2Options>(options => Configure(options));
            services.PostConfigure<AttachmentScanningOptions>(options => { options.Host = content.ScannerHost; options.Port = content.ScannerPort; });
        }));
        using var api = host.CreateClient(); api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var issued = await api.PostAsJsonAsync($"/api/tickets/{ticket.Id}/attachments/upload-url", new { fileName = name, fileSizeBytes = bytes.Length, contentType = type });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        var grant = (await issued.Content.ReadFromJsonAsync<UploadUrlResult>())!;
        using var http = new HttpClient();
        using var payload = new ByteArrayContent(bytes); payload.Headers.ContentType = new MediaTypeHeaderValue(type);
        using var uploaded = await http.PutAsync(grant.UploadUrl, payload); Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        using var anonymous = await http.GetAsync($"{content.Endpoint}/{AttachmentContentFixture.Bucket}/{grant.StorageKey}");
        Assert.Equal(HttpStatusCode.Forbidden, anonymous.StatusCode);
        using var confirmed = await api.PostAsJsonAsync($"/api/tickets/{ticket.Id}/attachments/confirm", new { fileName = name, fileSizeBytes = bytes.Length, contentType = type, storageKey = grant.StorageKey });
        await using var verify = database.CreateContext();
        if (kind != "clean")
        {
            Assert.Equal(HttpStatusCode.BadRequest, confirmed.StatusCode);
            Assert.False(await verify.TicketAttachments.AnyAsync(a => a.TicketId == ticket.Id));
            Assert.Null((await verify.AttachmentUploadGrants.SingleAsync(g => g.StorageKey == grant.StorageKey)).AttachmentId);
            return;
        }
        Assert.Equal(HttpStatusCode.Created, confirmed.StatusCode);
        var attachment = (await confirmed.Content.ReadFromJsonAsync<TicketAttachmentDto>())!;
        Assert.NotEqual(grant.StorageKey, attachment.StorageKey);
        using var replay = await api.PostAsJsonAsync($"/api/tickets/{ticket.Id}/attachments/confirm", new { fileName = name, fileSizeBytes = bytes.Length, contentType = type, storageKey = grant.StorageKey });
        Assert.Equal(attachment.Id, (await replay.Content.ReadFromJsonAsync<TicketAttachmentDto>())!.Id);
        using var response = await api.GetAsync($"/api/tickets/{ticket.Id}/attachments/download-url?storageKey={Uri.EscapeDataString(attachment.StorageKey)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var url = (await response.Content.ReadFromJsonAsync<DownloadUrlResult>())!.DownloadUrl;
        using var downloaded = await http.GetAsync(url);
        Assert.Equal(bytes, await downloaded.Content.ReadAsByteArrayAsync());
        Assert.Equal("attachment", downloaded.Content.Headers.ContentDisposition?.DispositionType);
        using var unconfirmed = await api.GetAsync($"/api/tickets/{ticket.Id}/attachments/download-url?storageKey={Uri.EscapeDataString(grant.StorageKey)}");
        Assert.False(unconfirmed.IsSuccessStatusCode);
    }

    [Fact]
    public async Task APreviouslyWritableLegacyObjectCannotChangeTheScannedDownloadCopy()
    {
        var key = $"tickets/{Guid.NewGuid()}/legacy.txt";
        await PutAsync(key, "clean"u8.ToArray());
        using var storage = Storage();
        using var scanner = new ClamAvAttachmentScanner(storage, Options.Create(new AttachmentScanningOptions { Host = content.ScannerHost, Port = content.ScannerPort }));
        var metadata = new TicketAttachmentDto(Guid.NewGuid(), Guid.NewGuid(), "legacy.txt", 5, "text/plain", key, Guid.NewGuid(), DateTime.UtcNow);
        var safeKey = await scanner.PrepareDownloadAsync(metadata);
        var url = await storage.GetPresignedDownloadUrlAsync(AttachmentContentFixture.Bucket, safeKey, 300);
        await PutAsync(key, Eicar());
        using var http = new HttpClient();
        Assert.Equal("clean", await http.GetStringAsync(url));
        await Assert.ThrowsAsync<ArgumentException>(() => scanner.PrepareDownloadAsync(metadata));
    }

[Theory]
    [InlineData("after-head")]
    [InlineData("after-copy")]
    [InlineData("after-commit")]
    [InlineData("before-cleanup")]
    public async Task FailureInjectionAndFreshContextRecoverWithoutDeletingCommittedObjects(string phase)
    {
        var actor = Guid.NewGuid(); var now = DateTime.UtcNow;
        var ticket = Ticket.Create(actor, "Failure recovery", "Storage calls stay outside database transactions", TicketCategories.NET, now);
        var grant = AttachmentUploadGrant.CreatePending(ticket.Id, actor, "file.txt", 5, "text/plain", now.AddMinutes(15));
        await using (var setup = database.CreateContext()) { setup.Tickets.Add(ticket); setup.AttachmentUploadGrants.Add(grant); await setup.SaveChangesAsync(); }
        await PutAsync(grant.StorageKey, "clean"u8.ToArray());
        using var real = Storage();
        var injected = 0; var copies = 0;
        var storage = new Mock<IObjectStorageService>();
        storage.SetupGet(s => s.DefaultBucket).Returns(real.DefaultBucket);
        storage.SetupGet(s => s.DownloadExpirySeconds).Returns(300);
        storage.Setup(s => s.StatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string bucket, string key, CancellationToken ct) =>
            {
                var value = await real.StatAsync(bucket, key, ct);
                if (phase == "after-head" && Interlocked.Exchange(ref injected, 1) == 0) throw new IOException("injected HEAD failure");
                return value;
            });
        storage.Setup(s => s.CopyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string bucket, string source, string target, string etag, CancellationToken ct) =>
            {
                await real.CopyAsync(bucket, source, target, etag, ct); Interlocked.Increment(ref copies);
                if (phase == "after-copy" && Interlocked.Exchange(ref injected, 1) == 0) throw new IOException("injected copy response loss");
            });
        storage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string bucket, string key, CancellationToken ct) =>
            {
                if (phase == "before-cleanup" && Interlocked.Exchange(ref injected, 1) == 0) throw new IOException("injected cleanup failure");
                await real.DeleteAsync(bucket, key, ct);
            });
        var user = new Mock<ICurrentUser>(); user.SetupGet(u => u.IsAuthenticated).Returns(true); user.SetupGet(u => u.UserId).Returns(actor.ToString()); user.SetupGet(u => u.Roles).Returns(new[] { "Requester" });
        using var scanner = new ClamAvAttachmentScanner(real, Options.Create(new AttachmentScanningOptions { Host = content.ScannerHost, Port = content.ScannerPort }));
        async Task<TicketAttachmentDto> Confirm(bool loseCommitResponse)
        {
            var builder = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString);
            if (loseCommitResponse) builder.AddInterceptors(new LostAttachmentCommitInterceptor());
            await using var context = new ApplicationDbContext(builder.Options);
            var useCase = new ConfirmAttachmentUploadUseCase(new PostgresTicketQueries(context), new PostgresTicketAttachmentStore(context), storage.Object, user.Object, new SystemClock(), scanner);
            return await useCase.ExecuteAsync(new(ticket.Id, new(grant.FileName, grant.FileSizeBytes, grant.ContentType, grant.StorageKey)));
        }
        if (phase == "after-commit") await Assert.ThrowsAsync<IOException>(() => Confirm(true));
        else if (phase == "before-cleanup") await Confirm(false);
        else await Assert.ThrowsAsync<ServiceUnavailableException>(() => Confirm(false));
        var recovered = await Confirm(false);
        await using var verify = database.CreateContext();
        Assert.Equal(recovered.Id, (await verify.TicketAttachments.SingleAsync(value => value.TicketId == ticket.Id)).Id);
        Assert.Equal(phase == "after-copy" ? 2 : 1, copies);
        var objects = await real.ListAsync(real.DefaultBucket, $"tickets/{ticket.Id}/", null);
        Assert.Contains(objects.Objects, value => value.Key == recovered.StorageKey);
        var cleanupStore = new PostgresAttachmentReconciliationStore(verify);
        Assert.False(await cleanupStore.MayDeleteObjectAsync(recovered.StorageKey));
        // Chỉ giả lập tuổi object, giữ storage/DB/delete thật và không ngủ chờ retention.
        storage.Setup(s => s.ListAsync(real.DefaultBucket, "tickets/", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageObjectPage(objects.Objects.Select(value => value with { LastModifiedUtc = DateTime.UtcNow.AddDays(-2) }).ToArray(), null));
        var reconciler = new ReconcileAttachmentsUseCase(cleanupStore, storage.Object);
        var cleaned = await reconciler.ExecuteAsync(new("tickets/", null, TimeSpan.FromDays(1)));
        Assert.Equal(phase == "after-copy" ? 1 : 0, cleaned.Deleted);
        Assert.NotNull(await real.StatAsync(real.DefaultBucket, recovered.StorageKey));
        Assert.Equal(0, (await reconciler.ExecuteAsync(new("tickets/", null, TimeSpan.FromDays(1)))).Failed);
        if (phase == "before-cleanup")
        {
            Assert.NotNull(await real.StatAsync(real.DefaultBucket, grant.StorageKey));
            Assert.True(await cleanupStore.MayDeleteObjectAsync(grant.StorageKey));
        }
    }

    [Fact]
    public async Task ConditionalCopyAndReadRejectAChangedSource()
    {
        using var storage = Storage();
        var key = $"uploads/pending/{Guid.NewGuid()}";
        await PutAsync(key, "clean"u8.ToArray());
        var original = (await storage.StatAsync(AttachmentContentFixture.Bucket, key))!;
        await PutAsync(key, "other"u8.ToArray());
        await Assert.ThrowsAsync<ArgumentException>(() => storage.CopyAsync(AttachmentContentFixture.Bucket, key, $"tickets/{Guid.NewGuid()}/copy", original.ETag));
        var error = await Assert.ThrowsAsync<Amazon.S3.AmazonS3Exception>(() => storage.ReadAsync(AttachmentContentFixture.Bucket, key, original.ETag, 10485760));
        Assert.Equal(HttpStatusCode.PreconditionFailed, error.StatusCode);
        var current = (await storage.StatAsync(AttachmentContentFixture.Bucket, key))!;
        await Assert.ThrowsAsync<ArgumentException>(() => storage.ReadAsync(AttachmentContentFixture.Bucket, key, current.ETag, 4));
    }

    private R2ObjectStorageService Storage()
    {
        var options = new R2Options(); Configure(options);
        return new R2ObjectStorageService(Options.Create(options), NullLogger<R2ObjectStorageService>.Instance);
    }
    private void Configure(R2Options options)
    {
        options.Endpoint = content.Endpoint; options.Bucket = AttachmentContentFixture.Bucket;
        options.AccessKeyId = AttachmentContentFixture.TestAccess; options.SecretAccessKey = AttachmentContentFixture.TestSecret;
        options.UploadExpirySeconds = 900; options.DownloadExpirySeconds = 300;
    }
    private async Task PutAsync(string key, byte[] bytes)
    {
        using var data = new MemoryStream(bytes);
        await content.Client.PutObjectAsync(new PutObjectRequest { BucketName = AttachmentContentFixture.Bucket, Key = key, InputStream = data, ContentType = "text/plain" });
    }
    // Chuỗi fixture được ghép trong RAM, không ghi mẫu antivirus ra filesystem của máy chủ.
    private static byte[] Eicar() => Encoding.ASCII.GetBytes(string.Concat(new[] { "X5O!P%@AP[4", "\\PZX54(P^)7CC)7}$", "EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*" }));
}
