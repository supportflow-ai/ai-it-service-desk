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
        var pubARes = await _client.PostAsJsonAsync($"/api/knowledge/articles/{draft.Id}/publish",
            new PublishArticleRequest(updatedDraft.Revision));
        pubARes.StatusCode.Should().Be(HttpStatusCode.OK);
        var published = await pubARes.Content.ReadFromJsonAsync<ArticleDto>();
        published!.Status.Should().Be(ArticleStatus.Published);
        published.Version.Should().Be(1);

        // 10. Requester reads article by Id -> 200 OK (Title and Content match)
        SetAuth(requesterToken);
        var readPubRes = await _client.GetAsync($"/api/knowledge/articles/{draft.Id}");
        readPubRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var readArticle = await readPubRes.Content.ReadFromJsonAsync<ArticleDto>();
        readArticle!.Title.Should().Be(updatedTitle);
        readArticle.Content.Should().Be(updatedContent);

        // 11. Requester searches keyword -> 200 OK, article is returned in items
        var searchPubRes = await _client.GetAsync($"/api/knowledge/articles/search?query={uniqueKey}");
        searchPubRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var searchPubResult = await searchPubRes.Content.ReadFromJsonAsync<PagedArticleResult>();
        searchPubResult!.Items.Should().ContainSingle(a => a.Id == draft.Id);

        // 12. Agent B tries to archive -> 403 Forbidden
        SetAuth(agentBToken);
        var archBRes = await _client.PostAsJsonAsync($"/api/knowledge/articles/{draft.Id}/archive",
            new ArchiveArticleRequest(published.Revision));
        archBRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 13. Requester tries to archive -> 403 Forbidden
        SetAuth(requesterToken);
        var archReqRes = await _client.PostAsJsonAsync($"/api/knowledge/articles/{draft.Id}/archive",
            new ArchiveArticleRequest(published.Revision));
        archReqRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 14. Agent A archives article with expectedRevision = published.Revision -> 200 OK
        SetAuth(agentAToken);
        var archARes = await _client.PostAsJsonAsync($"/api/knowledge/articles/{draft.Id}/archive",
            new ArchiveArticleRequest(published.Revision));
        archARes.StatusCode.Should().Be(HttpStatusCode.OK);
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
        var archRes = await _client.PostAsJsonAsync($"/api/knowledge/articles/{draft!.Id}/archive",
            new ArchiveArticleRequest(draft.Revision));
        archRes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // Agent publishes draft -> 200 OK
        var pubRes = await _client.PostAsJsonAsync($"/api/knowledge/articles/{draft.Id}/publish",
            new PublishArticleRequest(draft.Revision));
        pubRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var published = await pubRes.Content.ReadFromJsonAsync<ArticleDto>();

        // Agent tries to publish already published article -> 422 Unprocessable Entity
        var pubAgainRes = await _client.PostAsJsonAsync($"/api/knowledge/articles/{draft.Id}/publish",
            new PublishArticleRequest(published!.Revision));
        pubAgainRes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

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
