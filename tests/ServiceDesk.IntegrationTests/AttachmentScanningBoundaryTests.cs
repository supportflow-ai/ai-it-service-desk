using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class AttachmentScanningBoundaryTests(TicketIdempotencyPostgresFixture fixture) : IClassFixture<TicketIdempotencyPostgresFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DownloadWithoutAnAvailableScannerCannotIssueAUrlIncludingLegacyFiles(bool legacy)
    {
        var actor = Guid.NewGuid();
        var token = await fixture.CreateTokenAsync(actor);
        var ticket = Ticket.Create(actor, "Unscanned attachment", "Scanner dependency must fail closed", TicketCategories.NET, DateTime.UtcNow);
        var key = legacy ? $"tickets/{ticket.Id}/legacy.txt" : $"tickets/{ticket.Id}/attachments/{Guid.NewGuid()}";
        await using (var db = fixture.CreateContext())
        {
            db.Tickets.Add(ticket);
            db.TicketAttachments.Add(TicketAttachment.Create(ticket.Id, "clean.txt", 5, "text/plain", key, actor, DateTime.UtcNow));
            await db.SaveChangesAsync();
        }
        var storage = Storage();
        await using var host = fixture.First!.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IObjectStorageService>();
            services.AddSingleton(storage.Object);
        }));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.GetAsync($"/api/tickets/{ticket.Id}/attachments/download-url?storageKey={Uri.EscapeDataString(key)}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        storage.Verify(s => s.GetPresignedDownloadUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmationWithoutAScannerLeavesTheGrantAndMetadataUnconfirmed()
    {
        var actor = Guid.NewGuid();
        var token = await fixture.CreateTokenAsync(actor);
        var ticket = Ticket.Create(actor, "Unscanned upload", "Unscanned upload must stay quarantined", TicketCategories.NET, DateTime.UtcNow);
        var grant = AttachmentUploadGrant.CreatePending(ticket.Id, actor, "clean.txt", 5, "text/plain", DateTime.UtcNow.AddMinutes(15));
        await using (var db = fixture.CreateContext())
        {
            db.Tickets.Add(ticket); db.AttachmentUploadGrants.Add(grant); await db.SaveChangesAsync();
        }
        var storage = Storage();
        await using var host = fixture.Second!.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IObjectStorageService>(); services.AddSingleton(storage.Object);
        }));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/attachments/confirm", new { fileName = grant.FileName, fileSizeBytes = grant.FileSizeBytes, contentType = grant.ContentType, storageKey = grant.StorageKey });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await using var verify = fixture.CreateContext();
        Assert.False(await verify.TicketAttachments.AnyAsync(a => a.TicketId == ticket.Id));
        Assert.Null((await verify.AttachmentUploadGrants.SingleAsync(g => g.Id == grant.Id)).AttachmentId);
    }

    private static Mock<IObjectStorageService> Storage()
    {
        var storage = new Mock<IObjectStorageService>();
        storage.SetupGet(s => s.DefaultBucket).Returns("private-test");
        storage.SetupGet(s => s.UploadExpirySeconds).Returns(900);
        storage.SetupGet(s => s.DownloadExpirySeconds).Returns(300);
        storage.Setup(s => s.StatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new StoredObject(5, "text/plain", "etag"));
        storage.Setup(s => s.CopyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        storage.Setup(s => s.GetPresignedDownloadUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync("https://storage.test/unscanned");
        storage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return storage;
    }
}
