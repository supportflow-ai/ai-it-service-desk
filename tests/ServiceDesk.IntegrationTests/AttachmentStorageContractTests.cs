using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ServiceDesk.Platform.Options;
using ServiceDesk.Platform.Persistence;
using ServiceDesk.Platform.Storage;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Unit")]
public sealed class AttachmentStorageContractTests
{
    [Fact]
    public void MigrationSnapshotMatchesCurrentModel()
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=offline_schema_only;Username=unused;Password=unused").Options);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void CanonicalGrantMigrationFollowsPublishedGrantAndOnlyExtendsItsTable()
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=offline_schema_only;Username=unused;Password=unused").Options);
        var migrations = context.Database.GetMigrations().ToArray();
        var previous = Array.IndexOf(migrations, "20261103090000_AddAttachmentUploadGrant");
        var current = Array.IndexOf(migrations, "20261103090001_LinkAttachmentUploadGrant");
        Assert.True(previous >= 0 && current > previous);
        Assert.DoesNotContain(migrations, name => name.Contains("AddAttachmentUploadIntents", StringComparison.Ordinal));
        var sql = context.GetService<IMigrator>().GenerateScript(migrations[previous], migrations[current]);
        Assert.Contains("attachment_id", sql);
        Assert.Contains("HAVING COUNT(*) = 1", sql);
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(context.Model.GetEntityTypes(), entity => entity.ClrType.Name == "TicketAttachmentUpload");
    }

    [Fact]
    public async Task PresignedPut_UsesR2EndpointAndRequiredHeaders()
    {
        var options = new R2Options
        {
            Endpoint = "https://example.r2.cloudflarestorage.com",
            AccessKeyId = "synthetic-key",
            SecretAccessKey = "synthetic-secret-only-for-tests",
            Bucket = "configured-bucket"
        };
        using var adapter = new R2ObjectStorageService(Options.Create(options), NullLogger<R2ObjectStorageService>.Instance);
        var upload = await adapter.CreateUploadAsync(adapter.DefaultBucket, "tickets/test/pending/object", "image/png", 1024, DateTime.UtcNow.AddSeconds(900));
        Assert.Equal("https://example.r2.cloudflarestorage.com", new Uri(upload.Url).GetLeftPart(UriPartial.Authority));
        Assert.NotNull(upload.RequiredHeaders);
        Assert.True(upload.RequiredHeaders.ContainsKey("Content-Type"));
        Assert.Equal("image/png", upload.RequiredHeaders["Content-Type"]);
        Assert.True(upload.RequiredHeaders.ContainsKey("Content-Length"));
        Assert.Equal("1024", upload.RequiredHeaders["Content-Length"]);
        Assert.Contains("content-length", upload.Url, StringComparison.OrdinalIgnoreCase);
    }

[Fact]
    public async Task MinimumUploadExpiryIsPositiveAndDoesNotExtendTheGrant()
    {
        var options = new R2Options { Endpoint = "https://example.r2.cloudflarestorage.com", AccessKeyId = "synthetic-key", SecretAccessKey = "synthetic-secret-only-for-tests", Bucket = "private-test", UploadExpirySeconds = 2 };
        using var adapter = new R2ObjectStorageService(Options.Create(options), NullLogger<R2ObjectStorageService>.Instance);
        var deadline = DateTime.UtcNow.AddSeconds(2);
        var signed = await adapter.CreateUploadAsync(adapter.DefaultBucket, "minimum/file", "text/plain", 5, deadline);
        var query = QueryHelpers.ParseQuery(new Uri(signed.Url).Query);
        var seconds = int.Parse(query["X-Amz-Expires"].ToString(), CultureInfo.InvariantCulture);
        var at = DateTime.ParseExact(query["X-Amz-Date"].ToString(), "yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        Assert.InRange(seconds, 1, 2);
        Assert.True(at.AddSeconds(seconds) <= deadline);
    }

    [Fact]
    public async Task PresignedGet_SetsAttachmentContentDisposition()
    {
        var options = new R2Options
        {
            Endpoint = "https://example.r2.cloudflarestorage.com",
            AccessKeyId = "synthetic-key",
            SecretAccessKey = "synthetic-secret-only-for-tests",
            Bucket = "configured-bucket"
        };
        using var adapter = new R2ObjectStorageService(Options.Create(options), NullLogger<R2ObjectStorageService>.Instance);
        var url = await adapter.GetPresignedDownloadUrlAsync(adapter.DefaultBucket, "tickets/test/file.html", 3600);
        Assert.Contains("response-content-disposition=attachment", url, StringComparison.OrdinalIgnoreCase);
    }
}
