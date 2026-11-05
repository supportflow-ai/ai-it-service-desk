using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using ServiceDesk.Application.Knowledge.Indexing;
using ServiceDesk.Infrastructure.Knowledge.Indexing;
using ServiceDesk.Infrastructure.Options;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Unit")]
public sealed class KnowledgeIndexAdapterTests
{
    private static JsonObject Fixture()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        return JsonNode.Parse(File.ReadAllText(Path.Combine(root,
            "contracts/ai/knowledge-index/v1/fixtures.json")))!.AsObject();
    }

    private static KnowledgeIndexSnapshot Snapshot(JsonNode request) => new(
        Guid.Parse(request["requestId"]!.GetValue<string>()),
        Guid.Parse(request["articleId"]!.GetValue<string>()),
        Guid.Parse(request["articleVersionId"]!.GetValue<string>()),
        request["versionNumber"]!.GetValue<int>(), request["title"]!.GetValue<string>(),
        request["content"]!.GetValue<string>());

    private static HttpKnowledgeIndexPreparer Adapter(HttpMessageHandler handler) => new(new HttpClient(handler),
        Options.Create(new KnowledgeIndexOptions
        {
            Enabled = true, Endpoint = "http://ai.test", BearerToken = "synthetic-test-token", TimeoutSeconds = 1
        }));

    [Fact]
    public async Task PythonFixtureMatchesDotnetHashesAndWireContract()
    {
        var fixture = Fixture();
        var handler = new Handler(HttpStatusCode.OK, fixture["response"]!.ToJsonString());
        var snapshot = Snapshot(fixture["request"]!);
        Assert.Equal(fixture["request"]!["sourceHash"]!.GetValue<string>(), snapshot.SourceHash);
        var result = await Adapter(handler).PrepareAsync(snapshot, default);
        Assert.NotEmpty(result.Chunks);
        Assert.Equal("Bearer synthetic-test-token", handler.Authorization);
        Assert.Equal("http://ai.test/v1/knowledge-indexes:prepare", handler.Url);
        Assert.True(JsonNode.DeepEquals(fixture["request"], JsonNode.Parse(handler.Body!)));
    }

    [Theory]
    [InlineData("dimension")]
    [InlineData("null_vector")]
    [InlineData("null_chunk")]
    [InlineData("profile")]
    [InlineData("correlation")]
    [InlineData("offset")]
    [InlineData("missing_field")]
    [InlineData("unknown_field")]
    [InlineData("source")]
    public async Task InvalidProviderResultIsRejectedBeforePersistence(string fault)
    {
        var fixture = Fixture();
        var response = fixture["response"]!;
        switch (fault)
        {
            case "dimension": response["chunks"]![0]!["embedding"] = new JsonArray(1); break;
            case "null_vector": response["chunks"]![0]!["embedding"] = null; break;
            case "null_chunk": response["chunks"]![0] = null; break;
            case "profile": response["profile"]!["dimension"] = 768; break;
            case "correlation": response["requestId"] = Guid.NewGuid().ToString(); break;
            case "offset": response["chunks"]![0]!["startChar"] = -1; break;
            case "missing_field": response.AsObject().Remove("sourceHash"); break;
            case "unknown_field": response["unexpected"] = true; break;
            case "source": response["chunks"]![0]!["text"] = "different content"; break;
        }
        var exception = await Assert.ThrowsAsync<KnowledgeIndexException>(() =>
            Adapter(new Handler(HttpStatusCode.OK, response.ToJsonString()))
                .PrepareAsync(Snapshot(fixture["request"]!), default));
        Assert.False(exception.Retryable);
    }

    [Theory]
    [InlineData(400, false)]
    [InlineData(401, false)]
    [InlineData(413, false)]
    [InlineData(429, true)]
    [InlineData(503, true)]
    public async Task HttpErrorsHaveBoundedRetryPolicy(int status, bool retryable)
    {
        var exception = await Assert.ThrowsAsync<KnowledgeIndexException>(() =>
            Adapter(new Handler((HttpStatusCode)status, "sensitive provider details"))
                .PrepareAsync(Snapshot(Fixture()["request"]!), default));
        Assert.Equal(retryable, exception.Retryable);
        Assert.Equal($"provider_http_{status}", exception.Code);
    }

    [Fact]
    public async Task OversizedResponseIsRejected()
    {
        var exception = await Assert.ThrowsAsync<KnowledgeIndexException>(() =>
            Adapter(new Handler(HttpStatusCode.OK, new string('x', 8 * 1024 * 1024 + 1)))
                .PrepareAsync(Snapshot(Fixture()["request"]!), default));
        Assert.Equal("index_response_too_large", exception.Code);
    }

    private sealed class Handler(HttpStatusCode status, string response) : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public string? Url { get; private set; }
        public string? Authorization { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Body = await request.Content!.ReadAsStringAsync(ct);
            Url = request.RequestUri!.ToString();
            Authorization = request.Headers.Authorization?.ToString();
            return new(status) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }
}
