using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.Modules.Ticketing.Infrastructure;
using ServiceDesk.Platform.Persistence;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class TicketCommandConcurrencyTests(TicketIdempotencyPostgresFixture fixture) : IClassFixture<TicketIdempotencyPostgresFixture>
{
    [Theory]
    [InlineData("claim", "claim", TicketStatus.Submitted)]
    [InlineData("assign", "transfer", TicketStatus.Submitted)]
    [InlineData("assign", "unassign", TicketStatus.Assigned)]
    [InlineData("transfer", "resolve", TicketStatus.InProgress)]
    [InlineData("close", "reopen", TicketStatus.Resolved)]
    public async Task TwoHostsHaveOneMutationAndOneAuditForTheSameRevision(string firstCommand, string secondCommand, TicketStatus initial)
    {
        var firstActor = Guid.NewGuid();
        var secondActor = Guid.NewGuid();
        var firstToken = await AdminTokenAsync(firstActor);
        var secondToken = await AdminTokenAsync(secondActor);
        var now = DateTime.UtcNow;
        var ticket = Ticket.Create(firstActor, "Concurrent commands", "Concurrent command regression", TicketCategories.NET, now);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), now);
        if (initial == TicketStatus.Assigned) ticket.Assign(firstActor, now);
        if (initial is TicketStatus.InProgress or TicketStatus.Resolved) ticket.Claim(firstActor, now);
        if (initial == TicketStatus.Resolved) ticket.Resolve(now);
        var team = SupportTeam.Create($"RACE_{Guid.NewGuid():N}", "Target team");
        uint revision;
        await using (var db = fixture.CreateContext())
        {
            db.SupportTeams.Add(team);
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();
            revision = db.Entry(ticket).Property<uint>("xmin").CurrentValue;
        }
        var rendezvous = new BeforeTicketSave(ticket.Id);
        await using var firstHost = fixture.First!.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(rendezvous))));
        await using var secondHost = fixture.Second!.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(rendezvous))));
        using var first = firstHost.CreateClient();
        using var second = secondHost.CreateClient();
        first.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstToken);
        second.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondToken);
        async Task<HttpResponseMessage> SendAsync(HttpClient client, string command) => command switch
        {
            "assign" => await client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/assign", new { agentId = secondActor, expectedRevision = revision }),
            "transfer" => await client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/transfer", new { targetTeamId = team.Id, reason = "Race", expectedRevision = revision }),
            _ => await client.PostAsync($"/api/tickets/{ticket.Id}/{command}?expectedRevision={revision}", null)
        };
        var responses = await Task.WhenAll(SendAsync(first, firstCommand), SendAsync(second, secondCommand));
        try
        {
            Assert.Equal(2, rendezvous.Arrivals);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            await using var verify = fixture.CreateContext();
            Assert.Single(await verify.TicketComments.Where(c => c.TicketId == ticket.Id).ToListAsync());
            var saved = await verify.Tickets.SingleAsync(t => t.Id == ticket.Id);
            var winner = responses[0].IsSuccessStatusCode ? firstCommand : secondCommand;
            Assert.Equal(winner switch
            {
                "claim" or "reopen" => TicketStatus.InProgress,
                "assign" => TicketStatus.Assigned,
                "unassign" => TicketStatus.Submitted,
                "resolve" => TicketStatus.Resolved,
                "close" => TicketStatus.Closed,
                _ => initial
            }, saved.Status);
            // Mất response ở client: retry mutation bằng revision cũ không ghi lần hai.
            using var retry = await SendAsync(second, winner);
            Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
            Assert.Equal(1, await verify.TicketComments.CountAsync(c => c.TicketId == ticket.Id));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task UpdateCannotRaceResolutionIntoAnInvalidState()
    {
        var id = Guid.NewGuid();
        var token = await AdminTokenAsync(id);
        var now = DateTime.UtcNow;
        var ticket = Ticket.Create(id, "Resolution race", "Update cannot edit in progress", TicketCategories.NET, now);
        ticket.Claim(id, now);
        uint revision;
        await using (var db = fixture.CreateContext())
        {
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();
            revision = db.Entry(ticket).Property<uint>("xmin").CurrentValue;
        }
        using var first = fixture.First!.CreateClient();
        using var second = fixture.Second!.CreateClient();
        first.DefaultRequestHeaders.Authorization = second.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var results = await Task.WhenAll(
            first.PostAsJsonAsync($"/api/tickets/{ticket.Id}/update", new { title = "Unexpected", description = "Should not be persisted", categoryId = "NET", expectedRevision = revision }),
            second.PostAsync($"/api/tickets/{ticket.Id}/resolve?expectedRevision={revision}", null));
        try
        {
            Assert.Contains(results[0].StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.Conflict });
            Assert.Equal(HttpStatusCode.OK, results[1].StatusCode);
            await using var verify = fixture.CreateContext();
            Assert.Equal(TicketStatus.Resolved, (await verify.Tickets.SingleAsync(t => t.Id == ticket.Id)).Status);
            Assert.Equal("Resolution race", (await verify.Tickets.SingleAsync(t => t.Id == ticket.Id)).Title);
            Assert.Equal(1, await verify.TicketComments.CountAsync(c => c.TicketId == ticket.Id));
        }
        finally { foreach (var response in results) response.Dispose(); }
    }

    private async Task<string> AdminTokenAsync(Guid id)
    {
        var token = await fixture.CreateTokenAsync(id, "Admin");
        await using var db = fixture.CreateContext();
        var role = await db.Roles.SingleAsync(r => r.NormalizedName == "ADMIN");
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = id.ToString(), RoleId = role.Id });
        await db.SaveChangesAsync();
        return token;
    }

    private sealed class BeforeTicketSave(Guid id) : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;
        public int Arrivals => Volatile.Read(ref _arrivals);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Ticket>().Any(e => e.Entity.Id == id && e.State == EntityState.Modified))
            {
                if (Interlocked.Increment(ref _arrivals) == 2) _ready.TrySetResult();
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }
}
