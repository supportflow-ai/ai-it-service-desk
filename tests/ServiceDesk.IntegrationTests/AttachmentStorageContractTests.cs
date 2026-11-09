using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Minio;
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
    public async Task SignedPostPolicy_UsesPublicOriginAndExactSize()
    {
        var options = new MinioOptions { Endpoint = "minio:9000", PublicEndpoint = "https://files.example.invalid",
            AccessKey = "synthetic-key", SecretKey = "synthetic-secret-only-for-tests", Bucket = "configured-bucket" };
        using var adapter = new MinioObjectStorageService(new Mock<IMinioClient>().Object,
            NullLogger<MinioObjectStorageService>.Instance, Options.Create(options));
        var upload = await adapter.CreateUploadAsync(adapter.DefaultBucket, "tickets/test/pending/object", "image/png", 1024, 3600);
        Assert.Equal("https://files.example.invalid", new Uri(upload.Url).GetLeftPart(UriPartial.Authority));
        Assert.Equal("tickets/test/pending/object", upload.FormData["key"]);
        using var policy = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(upload.FormData["policy"])));
        Assert.Contains(policy.RootElement.GetProperty("conditions").EnumerateArray(), condition =>
            condition.ValueKind == JsonValueKind.Array && condition[0].GetString() == "content-length-range" &&
            // SDK MinIO 6.0.5 emits integer strings; MinIO's toInteger accepts both forms.
            // Keep checking exact bounds rather than assuming a JSON representation.
            long.Parse(condition[1].ToString(), System.Globalization.CultureInfo.InvariantCulture) == 1024 &&
            long.Parse(condition[2].ToString(), System.Globalization.CultureInfo.InvariantCulture) == 1024);
    }
}
