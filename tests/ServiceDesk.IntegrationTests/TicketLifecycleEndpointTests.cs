using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.CreateTicket;
using ServiceDesk.Domain.Ticketing;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public class TicketLifecycleEndpointTests : IClassFixture<ServiceDeskWebApplicationFactory>
{
    private readonly ServiceDeskWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TicketLifecycleEndpointTests(ServiceDeskWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UpdateTicket_Submitted_ReturnsOk()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var ticket = await CreateTicketAsync();

        var updateResponse = await _client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/update", new
        {
            title = "Updated Title",
            description = "Updated description at least 10 chars",
            categoryId = TicketCategories.HW
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<TicketDto>();
        updated!.Title.Should().Be("Updated Title");
        updated.CategoryId.Should().Be(TicketCategories.HW);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task CancelTicket_Submitted_TransitionsToCancelled()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var ticket = await CreateTicketAsync();

        var cancelResponse = await _client.PostAsync($"/api/tickets/{ticket.Id}/cancel", null);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<TicketDto>();
        cancelled!.Status.Should().Be((int)TicketStatus.Cancelled);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task FullWorkflow_Claim_Resolve_Reopen_Close_WorksEndToEnd()
    {
        // 1. Requester creates ticket
        var requesterToken = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);
        var ticket = await CreateTicketAsync();
        ticket.Status.Should().Be((int)TicketStatus.Submitted);

        // 2. Register agent and promote
        var (agentToken, _) = await RegisterAndPromoteRoleAsync("Agent");

        // 3. Agent claims ticket
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var claimRes = await _client.PostAsync($"/api/tickets/{ticket.Id}/claim", null);
        claimRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var inProgressTicket = await claimRes.Content.ReadFromJsonAsync<TicketDto>();
        inProgressTicket!.Status.Should().Be((int)TicketStatus.InProgress);

        // 4. Agent resolves ticket
        var resolveRes = await _client.PostAsync($"/api/tickets/{ticket.Id}/resolve", null);
        resolveRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var resolvedTicket = await resolveRes.Content.ReadFromJsonAsync<TicketDto>();
        resolvedTicket!.Status.Should().Be((int)TicketStatus.Resolved);

        // 5. Requester reopens ticket
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);
        var reopenRes = await _client.PostAsync($"/api/tickets/{ticket.Id}/reopen", null);
        reopenRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var reopenedTicket = await reopenRes.Content.ReadFromJsonAsync<TicketDto>();
        reopenedTicket!.Status.Should().Be((int)TicketStatus.InProgress);

        // 6. Agent resolves ticket again
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var resolveAgain = await _client.PostAsync($"/api/tickets/{ticket.Id}/resolve", null);
        resolveAgain.StatusCode.Should().Be(HttpStatusCode.OK);

        // 7. Requester closes ticket
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);
        var closeRes = await _client.PostAsync($"/api/tickets/{ticket.Id}/close", null);
        closeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var closedTicket = await closeRes.Content.ReadFromJsonAsync<TicketDto>();
        closedTicket!.Status.Should().Be((int)TicketStatus.Closed);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    private async Task<(string Token, string UserId)> RegisterAndPromoteRoleAsync(string role)
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
        return (loginBody!.Token!, body.User.UserId);
    }

    private sealed record AuthResponseWithUser(string? Token, UserInfo? User);
    private sealed record UserInfo(string UserId, string Email, string? FullName, IReadOnlyList<string> Roles);

    private async Task<TicketDto> CreateTicketAsync()
    {
        var command = new CreateTicketCommand(
            Title: "VPN connection issue",
            Description: "Cannot connect from home network",
            CategoryId: TicketCategories.VPN
        );

        var response = await _client.PostAsJsonAsync("/api/tickets", command);
        response.EnsureSuccessStatusCode();
        var ticket = await response.Content.ReadFromJsonAsync<TicketDto>();
        return ticket!;
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
}
