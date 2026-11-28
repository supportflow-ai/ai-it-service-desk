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
public sealed class AttachmentAccessScopeTests(TicketIdempotencyPostgresFixture fixture) : IClassFixture<TicketIdempotencyPostgresFixture>
{
    [Fact]
    public async Task MetadataAndNewUrlsFollowTeamMembershipTransferOwnershipAndDirectAssignment()
    {
        var owner = Guid.NewGuid();
        var agentA = Guid.NewGuid();
        var leadA = Guid.NewGuid();
        var agentB = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var unrelated = Guid.NewGuid();
        var tokens = new Dictionary<Guid, string>();
        foreach (var (id, role) in new[] { (owner, "Requester"), (agentA, "Agent"), (leadA, "Agent"), (agentB, "Agent"), (admin, "Admin"), (unrelated, "Requester") })
            tokens[id] = await fixture.CreateTokenAsync(id, role);
        var teamA = SupportTeam.Create($"A_{Guid.NewGuid():N}", "Team A");
        var teamB = SupportTeam.Create($"B_{Guid.NewGuid():N}", "Team B");
        var now = DateTime.UtcNow;
        var ticket = Ticket.Create(owner, "Attachment access", "Attachment policy regression", TicketCategories.NET, now);
        ticket.TransferToTeam(teamA.Id, "Initial routing", false, now);
        var key = $"tickets/{ticket.Id}/attachments/{Guid.NewGuid():N}.pdf";
        await using (var db = fixture.CreateContext())
        {
            db.SupportTeams.AddRange(teamA, teamB);
            db.SupportTeamMembers.AddRange(SupportTeamMember.Create(teamA.Id, agentA), SupportTeamMember.Create(teamA.Id, leadA, true), SupportTeamMember.Create(teamB.Id, agentB));
            db.Tickets.Add(ticket);
            db.TicketAttachments.Add(TicketAttachment.Create(ticket.Id, "file.pdf", 20, "application/pdf", key, owner, now));
            await db.SaveChangesAsync();
        }
        var storage = new Mock<IObjectStorageService>(MockBehavior.Strict);
        storage.SetupGet(s => s.DefaultBucket).Returns("private-test");
        storage.SetupGet(s => s.DownloadExpirySeconds).Returns(300);
        var calls = 0;
        storage.Setup(s => s.GetPresignedDownloadUrlAsync("private-test", key, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback(() => Interlocked.Increment(ref calls)).ReturnsAsync("https://storage.test/private-download");
        await using var host = fixture.Second!.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IObjectStorageService>();
            services.AddSingleton(storage.Object);
        }));
        async Task CheckAsync(Guid actor, HttpStatusCode expected)
        {
            using var client = host.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens[actor]);
            var before = calls;
            using var metadata = await client.GetAsync($"/api/tickets/{ticket.Id}/attachments");
            Assert.Equal(expected, metadata.StatusCode);
            using var download = await client.GetAsync($"/api/tickets/{ticket.Id}/attachments/download-url?storageKey={Uri.EscapeDataString(key)}");
            Assert.Equal(expected, download.StatusCode);
            Assert.Equal(before + (expected == HttpStatusCode.OK ? 1 : 0), calls);
        }
        await CheckAsync(owner, HttpStatusCode.OK);
        await CheckAsync(agentA, HttpStatusCode.OK);
        await CheckAsync(leadA, HttpStatusCode.OK);
        await CheckAsync(admin, HttpStatusCode.OK);
        await CheckAsync(agentB, HttpStatusCode.NotFound);
        await CheckAsync(unrelated, HttpStatusCode.NotFound);
        await using (var db = fixture.CreateContext())
        {
            db.SupportTeamMembers.Remove(await db.SupportTeamMembers.SingleAsync(m => m.TeamId == teamA.Id && m.UserId == agentA));
            await db.SaveChangesAsync();
        }
        await CheckAsync(agentA, HttpStatusCode.NotFound);
        await using (var db = fixture.CreateContext())
        {
            (await db.SupportTeams.SingleAsync(t => t.Id == teamA.Id)).Update("Team A", false);
            await db.SaveChangesAsync();
        }
        await CheckAsync(leadA, HttpStatusCode.NotFound);
        await CheckAsync(owner, HttpStatusCode.OK);
        using (var client = host.CreateClient())
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens[admin]);
            await using var db = fixture.CreateContext();
            var current = await db.Tickets.SingleAsync(t => t.Id == ticket.Id);
            var revision = db.Entry(current).Property<uint>("xmin").CurrentValue;
            using var transfer = await client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/transfer", new { targetTeamId = teamB.Id, reason = "Route to B", expectedRevision = revision });
            Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);
        }
        await CheckAsync(leadA, HttpStatusCode.NotFound);
        await CheckAsync(agentB, HttpStatusCode.OK);
        await CheckAsync(owner, HttpStatusCode.OK);
        await using (var db = fixture.CreateContext())
        {
            var current = await db.Tickets.SingleAsync(t => t.Id == ticket.Id);
            current.Assign(agentA, DateTime.UtcNow);
            (await db.SupportTeams.SingleAsync(t => t.Id == teamB.Id)).Update("Team B", false);
            await db.SaveChangesAsync();
        }
        await CheckAsync(agentA, HttpStatusCode.OK);
        await CheckAsync(agentB, HttpStatusCode.NotFound);
        await CheckAsync(admin, HttpStatusCode.OK);
        using var ownClient = host.CreateClient();
        ownClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens[owner]);
        var oldCalls = calls;
        using var wrongKey = await ownClient.GetAsync($"/api/tickets/{ticket.Id}/attachments/download-url?storageKey=tickets/{Guid.NewGuid()}/foreign.pdf");
        Assert.Equal(HttpStatusCode.Forbidden, wrongKey.StatusCode);
        Assert.Equal(oldCalls, calls);
    }
}
