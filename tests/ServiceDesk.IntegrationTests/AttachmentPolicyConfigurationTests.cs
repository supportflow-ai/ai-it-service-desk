using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.Platform.Options;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class AttachmentPolicyConfigurationTests(TicketIdempotencyPostgresFixture fixture) : IClassFixture<TicketIdempotencyPostgresFixture>
{
    [Theory]
    [InlineData(900, 300)]
    [InlineData(137, 43)]
    public async Task ConfiguredTtlsBoundTheGrantAndBothSignedUrls(int uploadSeconds, int downloadSeconds)
    {
        var actor = Guid.NewGuid();
        var token = await fixture.CreateTokenAsync(actor);
        var ticket = Ticket.Create(actor, "Configured attachment TTL", "Configuration must control URL lifetime", TicketCategories.NET, DateTime.UtcNow);
        var key = $"tickets/{ticket.Id}/attachments/{Guid.NewGuid()}";
        await using (var db = fixture.CreateContext())
        {
            db.Tickets.Add(ticket);
            db.TicketAttachments.Add(TicketAttachment.Create(ticket.Id, "clean.txt", 5, "text/plain", key, actor, DateTime.UtcNow));
            await db.SaveChangesAsync();
        }
        await using var host = fixture.First!.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton<IAttachmentScanner, CleanAttachmentScannerStub>();
            services.PostConfigure<R2Options>(options =>
            {
                options.Endpoint = "https://example.r2.cloudflarestorage.com";
                options.Bucket = "private-test";
                options.AccessKeyId = "test-access-key";
                options.SecretAccessKey = "test-secret-only-for-local-signatures";
                options.UploadExpirySeconds = uploadSeconds;
                options.DownloadExpirySeconds = downloadSeconds;
            });
        }));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var before = DateTime.UtcNow;
        using var response = await client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/attachments/upload-url", new { fileName = "clean.txt", fileSizeBytes = 5, contentType = "text/plain" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var upload = (await response.Content.ReadFromJsonAsync<UploadUrlResult>())!;
        var after = DateTime.UtcNow;
        Assert.Equal(uploadSeconds, upload.ExpirySeconds);
        await using var verify = fixture.CreateContext();
        var grant = await verify.AttachmentUploadGrants.AsNoTracking().SingleAsync(g => g.ActorId == actor);
        Assert.InRange(grant.ExpiresAt, before.AddSeconds(uploadSeconds), after.AddSeconds(uploadSeconds));
        var uploadQuery = QueryHelpers.ParseQuery(new Uri(upload.UploadUrl).Query);
        var signedAt = DateTime.ParseExact(uploadQuery["X-Amz-Date"].ToString(), "yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        var signedSeconds = int.Parse(uploadQuery["X-Amz-Expires"].ToString(), CultureInfo.InvariantCulture);
        Assert.InRange(signedSeconds, uploadSeconds - 2, uploadSeconds);
        Assert.True(signedAt.AddSeconds(signedSeconds) <= grant.ExpiresAt);
        using var downloadResponse = await client.GetAsync($"/api/tickets/{ticket.Id}/attachments/download-url?storageKey={Uri.EscapeDataString(key)}");
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        var download = (await downloadResponse.Content.ReadFromJsonAsync<DownloadUrlResult>())!;
        var downloadQuery = QueryHelpers.ParseQuery(new Uri(download.DownloadUrl).Query);
        Assert.InRange(int.Parse(downloadQuery["X-Amz-Expires"].ToString(), CultureInfo.InvariantCulture), downloadSeconds - 2, downloadSeconds);
    }
}
