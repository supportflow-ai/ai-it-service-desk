using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Ticketing.Errors;
using ServiceDesk.Domain.Ticketing;
using ServiceDesk.Infrastructure.Clock;
using ServiceDesk.Infrastructure.Identity;
using ServiceDesk.Infrastructure.Options;
using ServiceDesk.Infrastructure.Persistence;
using ServiceDesk.Infrastructure.Ticketing;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class TicketConcurrencyPostgresTests(TicketIdempotencyPostgresFixture fixture)
    : IClassFixture<TicketIdempotencyPostgresFixture>
{
    [Theory]
    [InlineData("update")]
    [InlineData("claim")]
    [InlineData("resolve")]
    [InlineData("reopen")]
    [InlineData("close")]
    [InlineData("cancel")]
    public async Task StaleWriter_IsRejectedAndPreservesTheCommittedChange(string action)
    {
        var id = await InsertTicket(action);
        await using var firstContext = fixture.CreateContext();
        await using var secondContext = fixture.CreateContext();
        var first = new PostgresTicketWriter(firstContext);
        var second = new PostgresTicketWriter(secondContext);
        var winner = (await first.GetForUpdateAsync(id, default))!;
        var loser = (await second.GetForUpdateAsync(id, default))!;
        Change(winner, action, "Winning update");
        Change(loser, action, "Losing update");
        var saved = await first.UpdateTicketAsync(winner, default);

        var conflict = await Assert.ThrowsAsync<TicketConcurrencyException>(() => second.UpdateTicketAsync(loser, default));
        Assert.IsType<DbUpdateConcurrencyException>(conflict.InnerException);
        await using var check = fixture.CreateContext();
        var persisted = await check.Tickets.AsNoTracking().SingleAsync(ticket => ticket.Id == id);
        Assert.Equal(saved.Title, persisted.Title);
        Assert.Equal(saved.Status, (int)persisted.Status);
        Assert.Equal(winner.UpdatedAt, persisted.UpdatedAt);
    }

    [Fact]
    public async Task DeletedTicket_AlsoProducesControlledConflict()
    {
        var id = await InsertTicket("update");
        await using var staleContext = fixture.CreateContext();
        var writer = new PostgresTicketWriter(staleContext);
        var ticket = (await writer.GetForUpdateAsync(id, default))!;
        await using (var deleting = fixture.CreateContext())
            Assert.Equal(1, await deleting.Tickets.Where(value => value.Id == id).ExecuteDeleteAsync());
        Change(ticket, "update", "Must not resurrect ticket");

        await Assert.ThrowsAsync<TicketConcurrencyException>(() => writer.UpdateTicketAsync(ticket, default));
        await using var check = fixture.CreateContext();
        Assert.False(await check.Tickets.AnyAsync(value => value.Id == id));
    }

    [Fact]
    public async Task TwoApiHosts_ReturnOneSuccessAndOne409WithoutLostUpdate()
    {
        var id = await InsertTicket("update");
        var firstGate = new BeforeTicketSave();
        var secondGate = new BeforeTicketSave();
        await using var firstFactory = new TicketIdempotencyPostgresFixture.Factory(fixture.ConnectionString, firstGate);
        await using var secondFactory = new TicketIdempotencyPostgresFixture.Factory(fixture.ConnectionString, secondGate);
        using var first = firstFactory.CreateClient();
        using var second = secondFactory.CreateClient();
        var token = StaffToken();
        first.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        second.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Task<HttpResponseMessage>? firstRequest = null;
        Task<HttpResponseMessage>? secondRequest = null;
        try
        {
            firstRequest = Update(first, id, "First committed update", deadline.Token);
            await firstGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            secondRequest = Update(second, id, "Second stale update", deadline.Token);
            await secondGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            firstGate.Release.TrySetResult(true);
            using var success = await firstRequest;
            Assert.Equal(HttpStatusCode.OK, success.StatusCode);
            secondGate.Release.TrySetResult(true);
            using var rejected = await secondRequest;
            Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
            using var body = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
            Assert.Equal("error", Assert.Single(body.RootElement.EnumerateObject()).Name);
            Assert.Null(rejected.Headers.RetryAfter);
            await using var check = fixture.CreateContext();
            Assert.Equal("First committed update", (await check.Tickets.SingleAsync(ticket => ticket.Id == id)).Title);
        }
        finally
        {
            firstGate.Release.TrySetResult(true);
            secondGate.Release.TrySetResult(true);
            deadline.Cancel();
            foreach (var pending in new[] { firstRequest, secondRequest })
                if (pending is not null)
                {
                    try { (await pending).Dispose(); }
                    catch (OperationCanceledException) { }
                }
        }
    }

    private async Task<Guid> InsertTicket(string action)
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Concurrency test", "Description long enough", "NET", DateTime.UtcNow);
        TicketLifecycleBoundaryTests.PrepareState(ticket, action);
        await using var context = fixture.CreateContext();
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();
        return ticket.Id;
    }

    private static void Change(Ticket ticket, string action, string title)
    {
        // PostgreSQL lưu độ chính xác microsecond; dùng thời điểm cố định khác nhau cho hai bản ghi.
        var now = new DateTime(2026, 10, 23, 15, 0, title.StartsWith("Winning", StringComparison.Ordinal) ? 1 : 2, DateTimeKind.Utc);
        switch (action)
        {
            case "update": ticket.Update(title, "Updated description long enough", "NET", now); break;
            case "claim": ticket.Claim(now); break;
            case "resolve": ticket.Resolve(now); break;
            case "reopen": ticket.Reopen(now); break;
            case "close": ticket.Close(now); break;
            case "cancel": ticket.Cancel(now); break;
            default: throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    private static Task<HttpResponseMessage> Update(HttpClient client, Guid id, string title, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync($"/api/tickets/{id}/update", new { title, description = "Updated description long enough", categoryId = "NET" }, cancellationToken);

    private static string StaffToken() => new JwtTokenGenerator(Options.Create(new JwtOptions
    {
        SecretKey = TicketIdempotencyPostgresFixture.Secret, Issuer = "ServiceDesk", Audience = "ServiceDeskClient", ExpiryMinutes = 60
    }), new SystemClock()).GenerateToken(new UserInfo
    {
        UserId = Guid.NewGuid().ToString(), Email = "concurrency@test.invalid", Roles = ["Admin"]
    });

    private sealed class BeforeTicketSave : SaveChangesInterceptor
    {
        internal TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Ticket>().Any(entry => entry.State == EntityState.Modified))
            {
                Entered.TrySetResult(true);
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
