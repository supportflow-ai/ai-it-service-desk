using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Npgsql;
using ServiceDesk.Application.Ticketing.Commands;
using ServiceDesk.Application.Ticketing.Dtos;
using ServiceDesk.Application.Ticketing.Idempotency;
using ServiceDesk.Infrastructure.Identity;
using ServiceDesk.Infrastructure.Options;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class TicketIdempotencyPostgresTests : IClassFixture<TicketIdempotencyPostgresFixture>
{
    private readonly TicketIdempotencyPostgresFixture _fixture;
    private static readonly CreateTicketCommand Payload = new("Network access issue", "Cannot connect to VPN from home", "NET");

    public TicketIdempotencyPostgresTests(TicketIdempotencyPostgresFixture fixture) => _fixture = fixture;

    private static string Token(Guid user) => new JwtTokenGenerator(Options.Create(new JwtOptions
    {
        SecretKey = TicketIdempotencyPostgresFixture.Secret, Issuer = "ServiceDesk",
        Audience = "ServiceDeskClient", ExpiryMinutes = 60
    })).GenerateToken(new IdentityUser { Id = user.ToString(), Email = "requester@test.invalid" }, ["Requester"]);

    private HttpClient Client(bool second = false) =>
        (second ? _fixture.Second! : _fixture.First!).CreateClient();

    private static async Task<HttpResponseMessage> Post(
        HttpClient client, Guid user, string? key, CreateTicketCommand? payload = null, bool authenticated = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/tickets");
        if (authenticated) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(user));
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        request.Content = JsonContent.Create(payload ?? Payload);
        return await client.SendAsync(request);
    }

    private async Task AssertCounts(Guid user, int tickets, int receipts)
    {
        await using var context = _fixture.CreateContext();
        (await context.Tickets.CountAsync(ticket => ticket.RequesterId == user)).Should().Be(tickets);
        (await context.TicketIdempotencyRecords.CountAsync(record => record.RequesterId == user)).Should().Be(receipts);
    }

    [Fact]
    public async Task T01_Create_T02_DifferentRequesterSameKeyCannotReplayOrReadOtherTicket()
    {
        var userA = Guid.NewGuid(); var userB = Guid.NewGuid(); var key = Guid.NewGuid().ToString();
        using var client = Client();
        using var first = await Post(client, userA, key);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var ticketA = (await first.Content.ReadFromJsonAsync<TicketDto>())!;
        using var second = await Post(client, userB, key, Payload with { Title = "Requester B private ticket" });
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var ticketB = (await second.Content.ReadFromJsonAsync<TicketDto>())!;
        ticketB.Id.Should().NotBe(ticketA.Id);
        ticketB.Title.Should().Be("Requester B private ticket");
        using var getOther = new HttpRequestMessage(HttpMethod.Get, $"/api/tickets/{ticketA.Id}");
        getOther.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(userB));
        using var forbiddenRead = await client.SendAsync(getOther);
        forbiddenRead.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertCounts(userA, 1, 1); await AssertCounts(userB, 1, 1);
    }

    [Fact]
    public async Task T03_SamePayloadReplaysExactOriginalResponse_EvenAfterTicketDataChanges()
    {
        var user = Guid.NewGuid(); var key = Guid.NewGuid().ToString();
        using var firstClient = Client(); using var secondClient = Client(true);
        using var first = await Post(firstClient, user, key);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var originalBody = await first.Content.ReadAsStringAsync();
        await using (var context = _fixture.CreateContext())
        {
            var ticket = await context.Tickets.SingleAsync(ticket => ticket.RequesterId == user);
            ticket.Title = "Changed after create";
            await context.SaveChangesAsync();
        }
        using var replay = await Post(secondClient, user, key);
        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        (await replay.Content.ReadAsStringAsync()).Should().Be(originalBody);
        replay.Headers.Location.Should().Be(first.Headers.Location);
        await AssertCounts(user, 1, 1);
    }

    [Fact]
    public async Task T04_SameScopeDifferentPayloadReturns409WithoutAnotherTicket()
    {
        var user = Guid.NewGuid(); var key = Guid.NewGuid().ToString();
        using var client = Client();
        using var first = await Post(client, user, key);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        using var conflict = await Post(client, user, key, Payload with { CategoryId = "ACC" });
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await conflict.Content.ReadAsStringAsync()).Should().NotContain(Payload.Description);
        await AssertCounts(user, 1, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task T05_ConcurrentSameKey_T06_TwoIndependentApiHosts_CreateOnlyOneTicket(bool twoHosts)
    {
        var user = Guid.NewGuid(); var key = Guid.NewGuid().ToString();
        using var firstClient = Client(); using var secondClient = Client(true);
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var gate = await connection.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@scope)", connection, gate))
        {
            command.Parameters.AddWithValue("scope", CreateTicketFingerprint.Create(user, key, Payload).LockId);
            await command.ExecuteNonQueryAsync();
        }
        // Hold the same database lock so all requests overlap before the first writer can commit.
        var requests = Enumerable.Range(0, 8).Select(index =>
            Post(twoHosts && index % 2 != 0 ? secondClient : firstClient, user, key)).ToArray();
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(4);
            long waiting = 0;
            while (waiting < 2 && DateTime.UtcNow < deadline)
            {
                await using var query = new NpgsqlCommand(
                    "SELECT count(*) FROM pg_locks WHERE locktype = 'advisory' AND NOT granted", connection, gate);
                waiting = (long)(await query.ExecuteScalarAsync())!;
                if (waiting < 2) await Task.Delay(25);
            }
            waiting.Should().BeGreaterThanOrEqualTo(2, "requests must actually overlap while waiting on PostgreSQL");
        }
        finally { await gate.CommitAsync(); }
        var responses = await Task.WhenAll(requests);
        try
        {
            var ids = new List<Guid>();
            foreach (var response in responses)
            {
                response.StatusCode.Should().Be(HttpStatusCode.Created);
                ids.Add((await response.Content.ReadFromJsonAsync<TicketDto>())!.Id);
            }
            ids.Distinct().Should().ContainSingle();
            await AssertCounts(user, 1, 1);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task T06_ConcurrentChangedPayloadAcrossHosts_HasOneWinnerAndOneConflict()
    {
        var user = Guid.NewGuid(); var key = Guid.NewGuid().ToString();
        using var firstClient = Client(); using var secondClient = Client(true);
        var responses = await Task.WhenAll(Post(firstClient, user, key),
            Post(secondClient, user, key, Payload with { Title = "A different payload" }));
        try
        {
            responses.Select(response => response.StatusCode).Should().BeEquivalentTo(
                [HttpStatusCode.Created, HttpStatusCode.Conflict]);
            await AssertCounts(user, 1, 1);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task T07_ReceiptWriteFailureRollsBackTicket_AndSameKeyCanRetry()
    {
        var user = Guid.NewGuid(); var key = Guid.NewGuid().ToString();
        // The trigger fails AFTER the ticket INSERT, at receipt INSERT, exercising transaction atomicity.
        await using (var context = _fixture.CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION public.reject_test_idempotency_receipt() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Injected receipt write failure'; END; $$;
                CREATE TRIGGER reject_test_idempotency_receipt
                BEFORE INSERT ON public.ticket_idempotency
                FOR EACH ROW EXECUTE FUNCTION public.reject_test_idempotency_receipt();
                """);
        }
        using var client = Client();
        try
        {
            using var failed = await Post(client, user, key);
            failed.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            await AssertCounts(user, 0, 0);
        }
        finally
        {
            await using var context = _fixture.CreateContext();
            await context.Database.ExecuteSqlRawAsync("""
                DROP TRIGGER reject_test_idempotency_receipt ON public.ticket_idempotency;
                DROP FUNCTION public.reject_test_idempotency_receipt();
                """);
        }
        using var retry = await Post(client, user, key);
        retry.StatusCode.Should().Be(HttpStatusCode.Created);
        await AssertCounts(user, 1, 1);
    }

    [Fact]
    public async Task T07_InvalidPayloadDoesNotBindKey()
    {
        var user = Guid.NewGuid(); var key = Guid.NewGuid().ToString();
        using var client = Client();
        using var invalid = await Post(client, user, key, Payload with { Title = "" });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertCounts(user, 0, 0);
        using var valid = await Post(client, user, key);
        valid.StatusCode.Should().Be(HttpStatusCode.Created);
        await AssertCounts(user, 1, 1);
    }

    [Fact]
    public async Task T08_ExpiredKeyStartsOneNewGeneration_AndThatGenerationReplays()
    {
        var user = Guid.NewGuid(); var key = Guid.NewGuid().ToString();
        using var client = Client(); using var secondClient = Client(true);
        using var first = await Post(client, user, key);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstId = (await first.Content.ReadFromJsonAsync<TicketDto>())!.Id;
        await using (var context = _fixture.CreateContext())
        {
            var receipt = await context.TicketIdempotencyRecords.SingleAsync(record => record.RequesterId == user);
            receipt.CreatedAt = DateTime.UtcNow.AddHours(-1);
            receipt.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await context.SaveChangesAsync();
        }
        var nextPayload = Payload with { Title = "New request after expiry" };
        var nextResponses = await Task.WhenAll(Post(secondClient, user, key, nextPayload),
            Post(client, user, key, nextPayload));
        using var next = nextResponses[0]; using var simultaneous = nextResponses[1];
        next.StatusCode.Should().Be(HttpStatusCode.Created);
        simultaneous.StatusCode.Should().Be(HttpStatusCode.Created);
        var newId = (await next.Content.ReadFromJsonAsync<TicketDto>())!.Id;
        newId.Should().NotBe(firstId);
        (await simultaneous.Content.ReadFromJsonAsync<TicketDto>())!.Id.Should().Be(newId);
        using var replay = await Post(client, user, key, nextPayload);
        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        (await replay.Content.ReadFromJsonAsync<TicketDto>())!.Id.Should().Be(newId);
        await AssertCounts(user, 2, 1);
    }

    [Fact]
    public async Task T09_UnauthenticatedCallerCannotReplayAnExistingKey()
    {
        var user = Guid.NewGuid(); var key = Guid.NewGuid().ToString();
        using var client = Client();
        using var first = await Post(client, user, key);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        using var unauthenticated = await Post(client, user, key, authenticated: false);
        unauthenticated.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await unauthenticated.Content.ReadAsStringAsync()).Should().NotContain(Payload.Description);
        await AssertCounts(user, 1, 1);
    }

    [Fact]
    public async Task T10_CreateWithoutKeyAndDetail_RetainExistingBehavior()
    {
        var user = Guid.NewGuid();
        using var client = Client();
        using var first = await Post(client, user, null);
        using var second = await Post(client, user, null);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var ticket = (await first.Content.ReadFromJsonAsync<TicketDto>())!;
        (await second.Content.ReadFromJsonAsync<TicketDto>())!.Id.Should().NotBe(ticket.Id);
        ticket.Status.Should().Be(1);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/tickets/{ticket.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(user));
        using var detail = await client.SendAsync(request);
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        (await detail.Content.ReadFromJsonAsync<TicketDto>()).Should().Be(ticket);
        await AssertCounts(user, 2, 0);
    }

    [Fact]
    public async Task LockTimeoutReturns503WithoutWrites_ThenSameKeyCanRetry()
    {
        var user = Guid.NewGuid(); var key = Guid.NewGuid().ToString();
        using var client = Client();
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var gate = await connection.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@scope)", connection, gate))
        {
            command.Parameters.AddWithValue("scope", CreateTicketFingerprint.Create(user, key, Payload).LockId);
            await command.ExecuteNonQueryAsync();
        }
        using var timedOut = await Post(client, user, key);
        timedOut.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        timedOut.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(1));
        await AssertCounts(user, 0, 0);
        await gate.CommitAsync();
        using var retry = await Post(client, user, key);
        retry.StatusCode.Should().Be(HttpStatusCode.Created);
        await AssertCounts(user, 1, 1);
    }

    [Fact]
    public async Task CorruptReceiptPointingToAnotherRequester_FailsClosedWithoutPrivateData()
    {
        var owner = Guid.NewGuid(); var other = Guid.NewGuid(); var key = Guid.NewGuid().ToString();
        using var client = Client();
        using var original = await Post(client, owner, key);
        original.StatusCode.Should().Be(HttpStatusCode.Created);
        await using (var context = _fixture.CreateContext())
        {
            var receipt = await context.TicketIdempotencyRecords.SingleAsync(record => record.RequesterId == owner);
            receipt.RequesterId = other; // Simulate a corrupt receipt; never a supported API operation.
            await context.SaveChangesAsync();
        }
        using var replay = await Post(client, other, key);
        replay.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await replay.Content.ReadAsStringAsync()).Should().NotContain(Payload.Description);
        await AssertCounts(owner, 1, 0);
        await AssertCounts(other, 0, 1);
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    public async Task T09_InvalidJwtCannotReplayAnExistingKey(string invalidPart)
    {
        var user = Guid.NewGuid(); var key = Guid.NewGuid().ToString();
        using var firstHost = Client();
        using var original = await Post(firstHost, user, key);
        original.StatusCode.Should().Be(HttpStatusCode.Created);

        var options = new JwtOptions
        {
            SecretKey = invalidPart == "signature" ? "different_test_secret_01234567890123456789" : TicketIdempotencyPostgresFixture.Secret,
            Issuer = invalidPart == "issuer" ? "different-test-issuer" : "ServiceDesk",
            Audience = invalidPart == "audience" ? "different-test-audience" : "ServiceDeskClient",
            ExpiryMinutes = invalidPart == "expired" ? -60 : 60
        };
        var token = new JwtTokenGenerator(Options.Create(options)).GenerateToken(
            new IdentityUser { Id = user.ToString(), Email = "requester@test.invalid" }, ["Requester"]);
        using var secondHost = Client(second: true);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/tickets");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Idempotency-Key", key);
        request.Content = JsonContent.Create(Payload);

        using var rejected = await secondHost.SendAsync(request);
        rejected.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await rejected.Content.ReadAsStringAsync()).Should().NotContain(Payload.Description);
        await AssertCounts(user, 1, 1);
    }
}
