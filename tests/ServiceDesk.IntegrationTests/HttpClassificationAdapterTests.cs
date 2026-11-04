using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ServiceDesk.Application.AIAssistance.Contracts;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Domain.AIAssistance;
using ServiceDesk.Domain.Ticketing;
using ServiceDesk.Infrastructure.AI;
using ServiceDesk.Infrastructure.Options;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Unit")]
public sealed class HttpClassificationAdapterTests
{
    private static readonly TriageRequest Snapshot = new()
    { TicketId = Guid.NewGuid(), TicketRevision = 7, Title = "VPN lỗi", Description = "Không thể kết nối" };
    private readonly TestClock _clock = new();
    private readonly AiOptions _options = new()
    { Enabled = true, Endpoint = "http://ai-service:8000", BearerToken = new string('x', 40), TimeoutSeconds = 1 };

    [Fact]
    public async Task SendsMinimalSnapshotAndValidatesResponseIdentity()
    {
        using var handler = new Handler(async (message, _) =>
        {
            Assert.Equal("http://ai-service:8000/v1/ticket-classifications", message.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", message.Headers.Authorization!.Scheme);
            Assert.Equal(_options.BearerToken, message.Headers.Authorization.Parameter);
            var request = JsonDocument.Parse(await message.Content!.ReadAsStringAsync()).RootElement;
            Assert.Equal(6, request.EnumerateObject().Count());
            Assert.Equal(InputHashComputer.Compute(Snapshot.Title, Snapshot.Description), request.GetProperty("inputHash").GetString());
            return Reply(request);
        });
        using var client = new HttpClient(handler);
        var result = await Adapter(client).ClassifyAsync(Snapshot);
        Assert.Equal(TriageOutcome.Success, result.Outcome);
        Assert.Equal(TicketCategory.VPN, result.SuggestedCategory);
    }

    [Theory]
    [InlineData("ticketId")]
    [InlineData("requestId")]
    [InlineData("ticketRevision")]
    [InlineData("inputHash")]
    [InlineData("unknown")]
    public async Task RejectsMismatchedOrExtendedClosedContract(string field)
    {
        using var handler = new Handler(async (message, _) =>
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(await message.Content!.ReadAsStringAsync())!;
            data.Remove("title"); data.Remove("description");
            data["suggestions"] = Array.Empty<object>();
            data[field] = field == "ticketRevision" ? 99 : field == "inputHash" ? new string('b', 64) : Guid.NewGuid().ToString();
            return Json(data);
        });
        using var client = new HttpClient(handler);
        Assert.Equal(TriageOutcome.InvalidResponse, (await Adapter(client).ClassifyAsync(Snapshot)).Outcome);
    }

    [Fact]
    public async Task RetriesTransientFailureOnceUsingSameRequestId()
    {
        var requests = new List<string>();
        using var handler = new Handler(async (message, _) =>
        {
            var text = await message.Content!.ReadAsStringAsync();
            requests.Add(text);
            return requests.Count == 1 ? new(HttpStatusCode.ServiceUnavailable) : Reply(JsonDocument.Parse(text).RootElement);
        });
        using var client = new HttpClient(handler);
        Assert.Equal(TriageOutcome.Success, (await Adapter(client).ClassifyAsync(Snapshot)).Outcome);
        Assert.Equal(2, requests.Count);
        Assert.Equal(requests[0], requests[1]);
    }

    [Fact]
    public async Task UnauthorizedIsNotRetriedAndRawErrorIsNotExposed()
    {
        var calls = 0;
        using var handler = new Handler((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            { Content = new StringContent("private provider credentials") });
        });
        using var client = new HttpClient(handler);
        var result = await Adapter(client).ClassifyAsync(Snapshot);
        Assert.Equal(1, calls);
        Assert.Equal("ai_http_unavailable", result.FailureReason);
        Assert.Null(result.SuggestedCategory);
    }

    [Fact]
    public async Task TimeoutIsControlledAndCallerCancellationPropagates()
    {
        using var handler = new Handler(async (_, token) =>
        { await Task.Delay(Timeout.InfiniteTimeSpan, token); return new(HttpStatusCode.OK); });
        using var client = new HttpClient(handler);
        Assert.Equal(TriageOutcome.Timeout, (await Adapter(client).ClassifyAsync(Snapshot)).Outcome);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Adapter(client).ClassifyAsync(Snapshot, cancelled.Token));
    }

    [Fact]
    public async Task BoundsChunkedResponseEvenWithoutContentLength()
    {
        using var handler = new Handler((_, _) =>
        {
            var body = new StreamContent(new NonSeekableBody(new byte[65_537]));
            body.Headers.ContentType = new("application/json");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = body });
        });
        using var client = new HttpClient(handler);
        var result = await Adapter(client).ClassifyAsync(Snapshot);
        Assert.Equal(TriageOutcome.InvalidResponse, result.Outcome);
        Assert.Equal("ai_response_too_large", result.FailureReason);
    }

    [Fact]
    public async Task OpensCircuitAfterThreeFailuresAndRecoversAfterCooldown()
    {
        var calls = 0;
        using var handler = new Handler((_, _) =>
        { calls++; throw new HttpRequestException("private-network-detail"); });
        using var client = new HttpClient(handler);
        var adapter = Adapter(client);
        for (var i = 0; i < 3; i++) Assert.Equal(TriageOutcome.Unavailable, (await adapter.ClassifyAsync(Snapshot)).Outcome);
        Assert.Equal("ai_circuit_open", (await adapter.ClassifyAsync(Snapshot)).FailureReason);
        Assert.Equal(3, calls);
        _clock.UtcNow = _clock.UtcNow.AddSeconds(31);
        Assert.Equal("ai_transport_unavailable", (await adapter.ClassifyAsync(Snapshot)).FailureReason);
        Assert.Equal(4, calls);
    }

    private HttpTicketClassificationService Adapter(HttpClient client) =>
        new(client, Options.Create(_options), new ClassificationCircuit(_clock));

    private static HttpResponseMessage Reply(JsonElement request) => Json(new
    {
        requestId = request.GetProperty("requestId").GetGuid(), ticketId = Snapshot.TicketId,
        ticketRevision = Snapshot.TicketRevision, inputHash = request.GetProperty("inputHash").GetString(),
        suggestions = new[] { new { field = "category", value = "VPN", score = 0.9 } }
    });
    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK)
    { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
    private sealed class TestClock : IClock
    { public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero); }
    private sealed class NonSeekableBody(byte[] bytes) : MemoryStream(bytes)
    { public override bool CanSeek => false; }
}
