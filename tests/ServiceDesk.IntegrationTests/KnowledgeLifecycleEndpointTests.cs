using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Api.Endpoints;
using ServiceDesk.Application.Knowledge.Contracts;
using ServiceDesk.Domain.Knowledge;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public class KnowledgeLifecycleEndpointTests : IClassFixture<ServiceDeskWebApplicationFactory>
{
    private readonly ServiceDeskWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public KnowledgeLifecycleEndpointTests(ServiceDeskWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FullWorkflow_Draft_Publish_Search_Archive_EnforcesNegativeVisibility_And_RBAC()
    {
        var agentAToken = await RegisterAndPromoteRoleAsync("Agent");
        var agentBToken = await RegisterAndPromoteRoleAsync("Agent");
        var requesterToken = await RegisterAndGetTokenAsync();

        var uniqueKey = Guid.NewGuid().ToString("N")[..8];
        var title = $"VPN Setup Guide {uniqueKey}";
        var content = "Comprehensive guide for setting up and connecting to corporate VPN.";
        var tags = new[] { "vpn", $"tag_{uniqueKey}" };

        // 1. Agent A registers and creates draft -> 201 Created
        SetAuth(agentAToken);
        var createResponse = await _client.PostAsJsonAsync("/api/knowledge/articles", new CreateDraftArticleRequest(title, content, tags));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var draft = await createResponse.Content.ReadFromJsonAsync<ArticleDto>();
        draft.Should().NotBeNull();
        draft!.Status.Should().Be(ArticleStatus.Draft);
        draft.Version.Should().Be(1);

        // 2. Requester reads article by Id -> 404 Not Found (KB-01 Negative Visibility for Draft)
        SetAuth(requesterToken);
        var readRes = await _client.GetAsync($"/api/knowledge/articles/{draft.Id}");
        readRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 3. Requester searches keyword -> 200 OK, items count == 0 (KB-02)
        var searchRes = await _client.GetAsync($"/api/knowledge/articles/search?query={uniqueKey}");
        searchRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var searchResult = await searchRes.Content.ReadFromJsonAsync<PagedArticleResult>();
        searchResult!.Items.Should().BeEmpty();

        // Agent B searches keyword -> 200 OK, items count == 0 (Agent B cannot see Agent A's draft)
        SetAuth(agentBToken);
        var searchBRes = await _client.GetAsync($"/api/knowledge/articles/search?query={uniqueKey}");
        searchBRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var searchBResult = await searchBRes.Content.ReadFromJsonAsync<PagedArticleResult>();
        searchBResult!.Items.Should().BeEmpty();

        // Agent A searches keyword -> 200 OK, draft is returned in items (Agent A can see own draft)
        SetAuth(agentAToken);
        var searchARes = await _client.GetAsync($"/api/knowledge/articles/search?query={uniqueKey}");
        searchARes.StatusCode.Should().Be(HttpStatusCode.OK);
        var searchAResult = await searchARes.Content.ReadFromJsonAsync<PagedArticleResult>();
        searchAResult!.Items.Should().ContainSingle(a => a.Id == draft.Id);

        // 4. Agent B tries to update Agent A's draft -> 403 Forbidden (KB-04)
        SetAuth(agentBToken);
        var updateBRes = await _client.PutAsJsonAsync($"/api/knowledge/articles/{draft.Id}/draft",
            new UpdateDraftArticleRequest("Updated by B", "Updated content at least 10 chars", tags, draft.Revision));
        updateBRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 5. Requester tries to update draft -> 403 Forbidden
        SetAuth(requesterToken);
        var updateReqRes = await _client.PutAsJsonAsync($"/api/knowledge/articles/{draft.Id}/draft",
            new UpdateDraftArticleRequest("Updated by Req", "Updated content at least 10 chars", tags, draft.Revision));
        updateReqRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 6. Agent A updates draft with expectedRevision = draft.Revision -> 200 OK
        SetAuth(agentAToken);
        var updatedTitle = $"VPN Setup Guide Updated {uniqueKey}";
        var updatedContent = "Updated content for setting up and connecting to corporate VPN.";
        var updateARes = await _client.PutAsJsonAsync($"/api/knowledge/articles/{draft.Id}/draft",
            new UpdateDraftArticleRequest(updatedTitle, updatedContent, tags, draft.Revision));
        updateARes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedDraft = await updateARes.Content.ReadFromJsonAsync<ArticleDto>();
        updatedDraft!.Title.Should().Be(updatedTitle);
        updatedDraft.Content.Should().Be(updatedContent);

        // 7. Agent B tries to publish Agent A's draft -> 403 Forbidden
        SetAuth(agentBToken);
        var pubBRes = await _client.PostAsJsonAsync($"/api/knowledge/articles/{draft.Id}/publish",
            new PublishArticleRequest(updatedDraft.Revision));
        pubBRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 8. Requester tries to publish draft -> 403 Forbidden
        SetAuth(requesterToken);
        var pubReqRes = await _client.PostAsJsonAsync($"/api/knowledge/articles/{draft.Id}/publish",
            new PublishArticleRequest(updatedDraft.Revision));
        pubReqRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 9. Agent A publishes draft with expectedRevision = updatedDraft.Revision -> 200 OK
        SetAuth(agentAToken);
        var pubARes = await _client.PostAsJsonAsync($"/api/knowledge/articles/{draft.Id}/publish?expectedRevision={updatedDraft.Revision}",
            new PublishArticleRequest(updatedDraft.Revision));
        pubARes.StatusCode.Should().Be(HttpStatusCode.OK, await pubARes.Content.ReadAsStringAsync());
        var published = await pubARes.Content.ReadFromJsonAsync<ArticleDto>();
        published!.Status.Should().Be(ArticleStatus.Published);
        published.Version.Should().Be(1);

        // 10. Requester reads article by Id -> 200 OK (Title and Content match)
        SetAuth(requesterToken);
        var readPubRes = await _client.GetAsync($"/api/knowledge/articles/{draft.Id}");
        readPubRes.StatusCode.Should().Be(HttpStatusCode.OK, await readPubRes.Content.ReadAsStringAsync());
        var readArticle = await readPubRes.Content.ReadFromJsonAsync<ArticleDto>();
        readArticle!.Title.Should().Be(updatedTitle);
        readArticle.Content.Should().Be(updatedContent);

        // 11. Requester searches keyword -> 200 OK, article is returned in items
        var searchPubRes = await _client.GetAsync($"/api/knowledge/articles/search?query={uniqueKey}");
        searchPubRes.StatusCode.Should().Be(HttpStatusCode.OK, await searchPubRes.Content.ReadAsStringAsync());
        var searchPubResult = await searchPubRes.Content.ReadFromJsonAsync<PagedArticleResult>();
        searchPubResult!.Items.Should().ContainSingle(a => a.Id == draft.Id);

        // 12. Agent B tries to archive -> 403 Forbidden
        SetAuth(agentBToken);
        var archBRes = await _client.PostAsJsonAsync($"/api/knowledge/articles/{draft.Id}/archive?expectedRevision={published.Revision}",
            new ArchiveArticleRequest(published.Revision));
        archBRes.StatusCode.Should().Be(HttpStatusCode.Forbidden, await archBRes.Content.ReadAsStringAsync());

        // 13. Requester tries to archive -> 403 Forbidden
        SetAuth(requesterToken);
        var archReqRes = await _client.PostAsJsonAsync($"/api/knowledge/articles/{draft.Id}/archive?expectedRevision={published.Revision}",
            new ArchiveArticleRequest(published.Revision));
        archReqRes.StatusCode.Should().Be(HttpStatusCode.Forbidden, await archReqRes.Content.ReadAsStringAsync());

        // 14. Agent A archives article with expectedRevision = published.Revision -> 200 OK
        SetAuth(agentAToken);
        var archARes = await _client.PostAsJsonAsync($"/api/knowledge/articles/{draft.Id}/archive?expectedRevision={published.Revision}",
            new ArchiveArticleRequest(published.Revision));
        archARes.StatusCode.Should().Be(HttpStatusCode.OK, await archARes.Content.ReadAsStringAsync());
        var archived = await archARes.Content.ReadFromJsonAsync<ArticleDto>();
        archived!.Status.Should().Be(ArticleStatus.Archived);

        // 15. Requester reads article by Id -> 404 Not Found (KB-01 Negative Visibility for Archived)
        SetAuth(requesterToken);
        var readArchRes = await _client.GetAsync($"/api/knowledge/articles/{draft.Id}");
        readArchRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 16. Requester searches keyword -> 200 OK, article is NOT in items
        var searchArchRes = await _client.GetAsync($"/api/knowledge/articles/search?query={uniqueKey}");
        searchArchRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var searchArchResult = await searchArchRes.Content.ReadFromJsonAsync<PagedArticleResult>();
        searchArchResult!.Items.Should().NotContain(a => a.Id == draft.Id);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task OptimisticConcurrency_MismatchedRevision_ReturnsConflict409()
    {
        var agentToken = await RegisterAndPromoteRoleAsync("Agent");
        SetAuth(agentToken);

        var createRes = await _client.PostAsJsonAsync("/api/knowledge/articles",
            new CreateDraftArticleRequest("Concurrency Test Article", "Valid content for concurrency testing", ["concurrency"]));
        createRes.EnsureSuccessStatusCode();
        var draft = await createRes.Content.ReadFromJsonAsync<ArticleDto>();

        var updateRes = await _client.PutAsJsonAsync($"/api/knowledge/articles/{draft!.Id}/draft",
            new UpdateDraftArticleRequest("Updated Title", "Valid content for concurrency testing", ["concurrency"], 999999));
        updateRes.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var pubRes = await _client.PostAsJsonAsync($"/api/knowledge/articles/{draft.Id}/publish",
            new PublishArticleRequest(999999));
        pubRes.StatusCode.Should().Be(HttpStatusCode.Conflict);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task InvalidStateTransitions_Return422Unprocessable()
    {
        var agentToken = await RegisterAndPromoteRoleAsync("Agent");
        SetAuth(agentToken);

        var createRes = await _client.PostAsJsonAsync("/api/knowledge/articles",
            new CreateDraftArticleRequest("State Transition Test", "Content for state transition testing", ["state"]));
        createRes.EnsureSuccessStatusCode();
        var draft = await createRes.Content.ReadFromJsonAsync<ArticleDto>();

        // Agent tries to archive a draft -> 422 Unprocessable Entity
        var archRes = await _client.PostAsJsonAsync($"/api/knowledge/articles/{draft!.Id}/archive?expectedRevision={draft.Revision}",
            new ArchiveArticleRequest(draft.Revision));
        archRes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await archRes.Content.ReadAsStringAsync());

        // Agent publishes draft -> 200 OK
        var pubRes = await _client.PostAsJsonAsync($"/api/knowledge/articles/{draft.Id}/publish?expectedRevision={draft.Revision}",
            new PublishArticleRequest(draft.Revision));
        pubRes.StatusCode.Should().Be(HttpStatusCode.OK, await pubRes.Content.ReadAsStringAsync());
        var published = await pubRes.Content.ReadFromJsonAsync<ArticleDto>();

        // Agent tries to publish already published article -> 422 Unprocessable Entity
        var pubAgainRes = await _client.PostAsJsonAsync($"/api/knowledge/articles/{draft.Id}/publish?expectedRevision={published!.Revision}",
            new PublishArticleRequest(published.Revision));
        pubAgainRes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await pubAgainRes.Content.ReadAsStringAsync());

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task Operations_WithoutExpectedRevision_Return400BadRequest()
    {
        var agentToken = await RegisterAndPromoteRoleAsync("Agent");
        SetAuth(agentToken);

        var createRes = await _client.PostAsJsonAsync("/api/knowledge/articles",
            new CreateDraftArticleRequest("Revision Validation Article", "Content at least 10 chars", ["revision"]));
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var draft = await createRes.Content.ReadFromJsonAsync<ArticleDto>();
        draft.Should().NotBeNull();

        // Agent A calls PUT /api/knowledge/articles/{draft.Id}/draft with 0 expectedRevision -> 400 BadRequest
        var updateZeroRes = await _client.PutAsJsonAsync($"/api/knowledge/articles/{draft!.Id}/draft",
            new UpdateDraftArticleRequest("T", "Content at least 10 chars", ["tag"], 0));
        updateZeroRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Agent A calls PUT /api/knowledge/articles/{draft.Id}/draft with no expectedRevision -> 400 BadRequest
        var updateNoneRes = await _client.PutAsJsonAsync($"/api/knowledge/articles/{draft.Id}/draft",
            new UpdateDraftArticleRequest("T", "Content at least 10 chars", ["tag"]));
        updateNoneRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Agent A calls POST /api/knowledge/articles/{draft.Id}/publish without expectedRevision -> 400 BadRequest
        var pubNoneRes = await _client.PostAsJsonAsync($"/api/knowledge/articles/{draft.Id}/publish",
            new PublishArticleRequest());
        pubNoneRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Agent A calls POST /api/knowledge/articles/{draft.Id}/publish?expectedRevision=0 -> 400 BadRequest
        var pubZeroRes = await _client.PostAsJsonAsync($"/api/knowledge/articles/{draft.Id}/publish?expectedRevision=0",
            new PublishArticleRequest(0));
        pubZeroRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Agent A publishes draft with valid expectedRevision = draft.Revision -> 200 OK
        var pubValidRes = await _client.PostAsJsonAsync($"/api/knowledge/articles/{draft.Id}/publish?expectedRevision={draft.Revision}",
            new PublishArticleRequest(draft.Revision));
        pubValidRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Agent A calls POST /api/knowledge/articles/{draft.Id}/archive without expectedRevision -> 400 BadRequest
        var archNoneRes = await _client.PostAsJsonAsync($"/api/knowledge/articles/{draft.Id}/archive",
            new ArchiveArticleRequest());
        archNoneRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Agent A calls POST /api/knowledge/articles/{draft.Id}/archive?expectedRevision=0 -> 400 BadRequest
        var archZeroRes = await _client.PostAsJsonAsync($"/api/knowledge/articles/{draft.Id}/archive?expectedRevision=0",
            new ArchiveArticleRequest(0));
        archZeroRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task RepublishAndRebuildEnforceOwnershipRevisionAndCurrentVersion()
    {
        var author = await RegisterAndPromoteRoleAsync("Agent");
        var other = await RegisterAndPromoteRoleAsync("Agent");
        var requester = await RegisterAndGetTokenAsync();
        var admin = await RegisterAndPromoteRoleAsync("Admin");
        SetAuth(author);
        var created = await _client.PostAsJsonAsync("/api/knowledge/articles",
            new CreateDraftArticleRequest("Index lifecycle", "First published snapshot.", ["vpn"]));
        created.EnsureSuccessStatusCode();
        var draft = (await created.Content.ReadFromJsonAsync<ArticleDto>())!;
        var path = $"/api/knowledge/articles/{draft.Id}";
        var publishedResponse = await _client.PostAsJsonAsync(path + "/publish", new PublishArticleRequest(draft.Revision));
        publishedResponse.EnsureSuccessStatusCode();
        var published = (await publishedResponse.Content.ReadFromJsonAsync<ArticleDto>())!;
        var republish = new RepublishArticleRequest("Index V2", "Second published snapshot.", ["vpn", "network"], published.Revision);
        var rebuild = new RebuildArticleIndexRequest(Guid.NewGuid(), published.Revision);
        foreach (var token in new[] { other, requester })
        {
            SetAuth(token);
            (await _client.PostAsJsonAsync(path + "/republish", republish)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await _client.PostAsJsonAsync(path + "/index/rebuild", rebuild)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await _client.GetAsync(path + "/index")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        SetAuth(author);
        (await _client.PostAsJsonAsync(path + "/republish", republish with { ExpectedRevision = 0 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.PostAsJsonAsync(path + "/republish", republish with { ExpectedRevision = uint.MaxValue }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        var v2Response = await _client.PostAsJsonAsync(path + "/republish", republish);
        v2Response.StatusCode.Should().Be(HttpStatusCode.OK, await v2Response.Content.ReadAsStringAsync());
        var v2 = (await v2Response.Content.ReadFromJsonAsync<ArticleDto>())!;
        v2.Version.Should().Be(2);
        v2.Tags.Should().BeEquivalentTo(["vpn", "network"]);
        (await _client.PostAsJsonAsync(path + "/index/rebuild", rebuild)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        SetAuth(admin);
        rebuild = rebuild with { ExpectedRevision = v2.Revision };
        var first = await _client.PostAsJsonAsync(path + "/index/rebuild", rebuild);
        first.StatusCode.Should().Be(HttpStatusCode.Accepted, await first.Content.ReadAsStringAsync());
        var replay = await _client.PostAsJsonAsync(path + "/index/rebuild", rebuild);
        replay.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await replay.Content.ReadAsStringAsync()).Should().Be(await first.Content.ReadAsStringAsync());
        var status = await _client.GetFromJsonAsync<ServiceDesk.Application.Knowledge.Indexing.KnowledgeIndexStatusDto>(path + "/index");
        status!.VersionNumber.Should().Be(2);
        status.Status.Should().Be("Pending");
        status.ActiveIndexSetId.Should().BeNull();
        SetAuth(requester);
        var visible = await _client.GetFromJsonAsync<ArticleDto>(path);
        visible!.Version.Should().Be(2);
        visible.Content.Should().Be(v2.Content);
        SetAuth(author);
        (await _client.PostAsJsonAsync(path + "/archive", new ArchiveArticleRequest(v2.Revision))).EnsureSuccessStatusCode();
        (await _client.PostAsJsonAsync(path + "/index/rebuild", rebuild)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await _client.PostAsJsonAsync(path + "/republish", republish with { ExpectedRevision = v2.Revision }))
            .StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity);
        _client.DefaultRequestHeaders.Authorization = null;
    }

    private void SetAuth(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> RegisterAndPromoteRoleAsync(string role)
    {
        var email = $"agent_user_{Guid.NewGuid():N}@example.com";
        var password = "Test@1234";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            fullName = "Agent Test User"
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponseWithUser>();

        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Microsoft.AspNetCore.Identity.IdentityUser>>();
        var user = await users.FindByIdAsync(body!.User!.UserId);
        await users.AddToRoleAsync(user!, role);

        var loginRes = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        loginRes.EnsureSuccessStatusCode();
        var loginBody = await loginRes.Content.ReadFromJsonAsync<AuthResponseWithUser>();
        return loginBody!.Token!;
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = $"lifecycle_user_{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Test@1234",
            fullName = "Lifecycle Test User"
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token!;
    }

    private sealed record AuthResponse(string? Token, object? User);
    private sealed record AuthResponseWithUser(string? Token, UserInfo? User);
    private sealed record UserInfo(string UserId, string Email, string? FullName, IReadOnlyList<string> Roles);
}
