using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ServiceDesk.Application.Knowledge.Indexing;
using ServiceDesk.Infrastructure.Options;

namespace ServiceDesk.Infrastructure.Knowledge.Indexing;

internal sealed class HttpKnowledgeIndexPreparer(HttpClient client, IOptions<KnowledgeIndexOptions> options)
    : IKnowledgeIndexPreparer
{
    private const int MaxResponseBytes = 8 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };

    public async Task<PreparedKnowledgeIndex> PrepareAsync(KnowledgeIndexSnapshot snapshot, CancellationToken cancellationToken)
    {
        var config = options.Value;
        if (!config.Enabled) throw new KnowledgeIndexException("index_provider_disabled", true);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));
        var ct = timeout.Token;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                new Uri(new Uri(config.Endpoint), "/v1/knowledge-indexes:prepare"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.BearerToken);
            request.Content = JsonContent.Create(new
            {
                snapshot.RequestId, snapshot.ArticleId, snapshot.ArticleVersionId, snapshot.VersionNumber,
                snapshot.SourceHash, ProfileId = KnowledgeIndexProfile.Id, snapshot.Title, snapshot.Content
            });
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode != HttpStatusCode.OK)
                throw new KnowledgeIndexException($"provider_http_{(int)response.StatusCode}",
                    response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500);
            if (response.Content.Headers.ContentLength > MaxResponseBytes)
                throw new KnowledgeIndexException("index_response_too_large", false);
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            var bytes = new byte[16_384];
            int read;
            while ((read = await stream.ReadAsync(bytes, ct)) > 0)
            {
                if (buffer.Length + read > MaxResponseBytes)
                    throw new KnowledgeIndexException("index_response_too_large", false);
                await buffer.WriteAsync(bytes.AsMemory(0, read), ct);
            }
            var wire = JsonSerializer.Deserialize<IndexResponseWire>(buffer.ToArray(), Json)
                ?? throw new KnowledgeIndexException("invalid_index_response", false);
            if (wire.Profile.ValueKind != JsonValueKind.Object)
                throw new KnowledgeIndexException("invalid_index_profile", false);
            var profile = new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var field in wire.Profile.EnumerateObject())
                if (!profile.TryAdd(field.Name, field.Value))
                    throw new KnowledgeIndexException("invalid_index_profile", false);
            if (KnowledgeIndexProfile.Hash(JsonSerializer.Serialize(profile)) != KnowledgeIndexProfile.Fingerprint)
                throw new KnowledgeIndexException("invalid_index_profile", false);
            var result = new PreparedKnowledgeIndex(wire.RequestId, wire.ArticleId, wire.ArticleVersionId,
                wire.VersionNumber, wire.SourceHash, wire.ProfileId, wire.ProfileHash,
                wire.NormalizedContentHash, wire.Chunks);
            result.Validate(snapshot);
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new KnowledgeIndexException("index_provider_timeout", true);
        }
        catch (HttpRequestException)
        {
            throw new KnowledgeIndexException("index_provider_unavailable", true);
        }
        catch (JsonException)
        {
            throw new KnowledgeIndexException("invalid_index_response", false);
        }
    }

    private sealed record IndexResponseWire(Guid RequestId, Guid ArticleId, Guid ArticleVersionId,
        int VersionNumber, string SourceHash, string ProfileId, string ProfileHash,
        JsonElement Profile, string NormalizedContentHash, PreparedKnowledgeChunk[] Chunks);
}
