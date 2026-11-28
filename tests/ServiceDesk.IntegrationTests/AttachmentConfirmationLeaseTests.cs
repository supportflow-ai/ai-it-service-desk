using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class AttachmentConfirmationLeaseTests(TicketIdempotencyPostgresFixture fixture) : IClassFixture<TicketIdempotencyPostgresFixture>
{
    [Fact]
    public async Task TwoHostsReserveConfirmationBeforeEitherCopiesTheObject()
    {
        var actor = Guid.NewGuid(); var token = await fixture.CreateTokenAsync(actor);
        var ticket = Ticket.Create(actor, "Confirmation lease", "Copy is outside the database transaction", TicketCategories.NET, DateTime.UtcNow);
        var grant = AttachmentUploadGrant.CreatePending(ticket.Id, actor, "evidence.txt", 5, "text/plain", DateTime.UtcNow.AddMinutes(15));
        await using (var setup = fixture.CreateContext()) { setup.Tickets.Add(ticket); setup.AttachmentUploadGrants.Add(grant); await setup.SaveChangesAsync(); }
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var copies = 0;
        var storage = new Mock<IObjectStorageService>();
        storage.SetupGet(s => s.DefaultBucket).Returns("private-test");
        storage.Setup(s => s.StatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new StoredObject(5, "text/plain", "etag"));
        storage.Setup(s => s.CopyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, string _, string _, string _, CancellationToken ct) =>
            {
                if (Interlocked.Increment(ref copies) == 1) { entered.SetResult(); await release.Task.WaitAsync(ct); }
            });
        void Configure(IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IObjectStorageService>(); services.AddSingleton(storage.Object);
            services.AddSingleton<IAttachmentScanner, CleanAttachmentScannerStub>();
        });
        await using var first = fixture.First!.WithWebHostBuilder(Configure);
        await using var second = fixture.Second!.WithWebHostBuilder(Configure);
        using var a = first.CreateClient(); using var b = second.CreateClient();
        a.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        b.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var body = new { fileName = grant.FileName, fileSizeBytes = grant.FileSizeBytes, contentType = grant.ContentType, storageKey = grant.StorageKey };
        var active = a.PostAsJsonAsync($"/api/tickets/{ticket.Id}/attachments/confirm", body);
        HttpResponseMessage competing;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            competing = await b.PostAsJsonAsync($"/api/tickets/{ticket.Id}/attachments/confirm", body);
        }
        finally { release.TrySetResult(); }
        using var completed = await active;
        using (competing)
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, competing.StatusCode);
            Assert.Equal("1", competing.Headers.RetryAfter?.Delta?.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        Assert.Equal(HttpStatusCode.Created, completed.StatusCode);
        Assert.Equal(1, copies);
        await using var verify = fixture.CreateContext();
        Assert.Single(await verify.TicketAttachments.Where(a => a.TicketId == ticket.Id).ToListAsync());
    }
}
