using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using ServiceDesk.Application.Auth;
using ServiceDesk.Application.Ticketing;
using ServiceDesk.Domain.Ticketing;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public class TicketEndpointsTests : IClassFixture<ServiceDeskWebApplicationFactory>
{
    private readonly ServiceDeskWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TicketEndpointsTests(ServiceDeskWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<AuthResult> RegisterUserAsync(string prefix = "user")
    {
        var email = $"{prefix}_{Guid.NewGuid():N}@example.com";
        var password = "ValidPassword123!";
        var response = await _client.PostAsJsonAsync("/auth/register", new RegisterRequest(email, password));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResult>();
        return auth!;
    }

    private async Task<AuthResult> CreateAgentAsync()
    {
        var userAuth = await RegisterUserAsync("agent_user");

        // Admin login and promote
        var adminLogin = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin@servicedesk.local", "AdminPass123!"));
        adminLogin.EnsureSuccessStatusCode();
        var adminAuth = await adminLogin.Content.ReadFromJsonAsync<AuthResult>();

        using var adminClient = CreateClientWithToken(adminAuth!.Token);
        var promoteResponse = await adminClient.PutAsJsonAsync($"/admin/users/{userAuth.User.Id}/role", new ChangeRoleRequest(ApplicationRoles.Agent));
        promoteResponse.EnsureSuccessStatusCode();

        // Refresh token by logging in again
        var reLogin = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(userAuth.User.Email, "ValidPassword123!"));
        reLogin.EnsureSuccessStatusCode();
        return (await reLogin.Content.ReadFromJsonAsync<AuthResult>())!;
    }

    private HttpClient CreateClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task RequesterAgentTicketFlow_ReopenAndClose_Succeeds()
    {
        var requester = await RegisterUserAsync("requester");
        var agent = await CreateAgentAsync();

        using var requesterClient = CreateClientWithToken(requester.Token);
        using var agentClient = CreateClientWithToken(agent.Token);

        // 1. Create ticket
        var createRequest = new CreateTicketRequest(
            "VPN connection fails",
            "Cannot connect to corporate VPN from home",
            "Network",
            Impact.High,
            Urgency.Medium);

        var createResponse = await requesterClient.PostAsJsonAsync("/tickets", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var ticket = await createResponse.Content.ReadFromJsonAsync<TicketResponse>();
        ticket.Should().NotBeNull();
        ticket!.Status.Should().Be(TicketStatus.Submitted);
        ticket.Priority.Should().Be(Priority.P2);
        ticket.RequesterId.Should().Be(requester.User.Id);
        ticket.AssigneeId.Should().BeNull();

        // 2. Agent claims ticket
        var claimResponse = await agentClient.PostAsync($"/tickets/{ticket.Id}/claim", null);
        claimResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var claimedTicket = await claimResponse.Content.ReadFromJsonAsync<TicketResponse>();
        claimedTicket!.Status.Should().Be(TicketStatus.InProgress);
        claimedTicket.AssigneeId.Should().Be(agent.User.Id);

        // 3. Agent resolves ticket
        var resolveResponse = await agentClient.PostAsync($"/tickets/{ticket.Id}/resolve", null);
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var resolvedTicket = await resolveResponse.Content.ReadFromJsonAsync<TicketResponse>();
        resolvedTicket!.Status.Should().Be(TicketStatus.Resolved);
        resolvedTicket.ResolvedAt.Should().NotBeNull();

        // 4. Requester reopens ticket
        var reopenResponse = await requesterClient.PostAsync($"/tickets/{ticket.Id}/reopen", null);
        reopenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reopenedTicket = await reopenResponse.Content.ReadFromJsonAsync<TicketResponse>();
        reopenedTicket!.Status.Should().Be(TicketStatus.InProgress);

        // 5. Agent resolves again
        var resolveAgainResponse = await agentClient.PostAsync($"/tickets/{ticket.Id}/resolve", null);
        resolveAgainResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 6. Requester closes ticket
        var closeResponse = await requesterClient.PostAsync($"/tickets/{ticket.Id}/close", null);
        closeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var closedTicket = await closeResponse.Content.ReadFromJsonAsync<TicketResponse>();
        closedTicket!.Status.Should().Be(TicketStatus.Closed);
        closedTicket.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RequesterCannotReadAnotherRequesterTicket_ReturnsNotFound()
    {
        var requester1 = await RegisterUserAsync("req1");
        var requester2 = await RegisterUserAsync("req2");

        using var client1 = CreateClientWithToken(requester1.Token);
        using var client2 = CreateClientWithToken(requester2.Token);

        var createResponse = await client1.PostAsJsonAsync("/tickets", new CreateTicketRequest(
            "Secret Ticket", "Private details", "General", Impact.Low, Urgency.Low));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var ticket = await createResponse.Content.ReadFromJsonAsync<TicketResponse>();

        // Requester 2 attempts to get Requester 1's ticket -> 404 Not Found
        var getResponse = await client2.GetAsync($"/tickets/{ticket!.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Requester 2 attempts to get comments -> 404 Not Found
        var getCommentsResponse = await client2.GetAsync($"/tickets/{ticket.Id}/comments");
        getCommentsResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Requester 2 lists tickets -> should not contain ticket of requester 1
        var listResponse = await client2.GetAsync("/tickets/me");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listPage = await listResponse.Content.ReadFromJsonAsync<PageResponse<TicketResponse>>();
        listPage!.Items.Should().NotContain(t => t.Id == ticket.Id);
    }

    [Fact]
    public async Task TicketComments_TerminalTicketComment_ReturnsBadRequest()
    {
        var requester = await RegisterUserAsync("comment_req");
        using var client = CreateClientWithToken(requester.Token);

        // Create ticket
        var createResponse = await client.PostAsJsonAsync("/tickets", new CreateTicketRequest(
            "Printer broken", "Office printer jam", "Hardware", Impact.Medium, Urgency.Medium));
        var ticket = await createResponse.Content.ReadFromJsonAsync<TicketResponse>();

        // Add valid comment
        var commentResponse = await client.PostAsJsonAsync($"/tickets/{ticket!.Id}/comments", new AddCommentRequest("Paper tray 2 is stuck"));
        commentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var comment = await commentResponse.Content.ReadFromJsonAsync<TicketCommentResponse>();
        comment.Should().NotBeNull();
        comment!.Content.Should().Be("Paper tray 2 is stuck");
        comment.AuthorId.Should().Be(requester.User.Id);

        // Get comments
        var getCommentsResponse = await client.GetAsync($"/tickets/{ticket.Id}/comments");
        getCommentsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var comments = await getCommentsResponse.Content.ReadFromJsonAsync<List<TicketCommentResponse>>();
        comments.Should().ContainSingle(c => c.Content == "Paper tray 2 is stuck");

        // Cancel ticket -> Terminal
        var cancelResponse = await client.PostAsync($"/tickets/{ticket.Id}/cancel", null);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Comment on cancelled ticket -> 400 Bad Request
        var failCommentResponse = await client.PostAsJsonAsync($"/tickets/{ticket.Id}/comments", new AddCommentRequest("Should fail"));
        failCommentResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AgentListWorkspace_AppliesStatusAndPagination()
    {
        var requester = await RegisterUserAsync("filter_req");
        var agent = await CreateAgentAsync();

        using var requesterClient = CreateClientWithToken(requester.Token);
        using var agentClient = CreateClientWithToken(agent.Token);

        // Create 2 tickets
        var t1 = await (await requesterClient.PostAsJsonAsync("/tickets", new CreateTicketRequest("T1", "Desc1", "IT", Impact.Low, Urgency.Low))).Content.ReadFromJsonAsync<TicketResponse>();
        var t2 = await (await requesterClient.PostAsJsonAsync("/tickets", new CreateTicketRequest("T2", "Desc2", "IT", Impact.High, Urgency.High))).Content.ReadFromJsonAsync<TicketResponse>();

        // Agent queries page 1 with pageSize 1 and status Submitted
        var queryResponse = await agentClient.GetAsync("/tickets/me?status=Submitted&page=1&pageSize=1");
        queryResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await queryResponse.Content.ReadFromJsonAsync<PageResponse<TicketResponse>>();
        page.Should().NotBeNull();
        page!.Page.Should().Be(1);
        page.PageSize.Should().Be(1);
        page.TotalCount.Should().BeGreaterThanOrEqualTo(2);
        page.Items.Should().HaveCount(1);
        page.Items.First().Status.Should().Be(TicketStatus.Submitted);
    }

    [Fact]
    public async Task UpdateTicket_SubmittedOnly_RecalculatesPriority()
    {
        var requester = await RegisterUserAsync("update_req");
        using var client = CreateClientWithToken(requester.Token);

        var createResponse = await client.PostAsJsonAsync("/tickets", new CreateTicketRequest(
            "Original Title", "Original Desc", "Hardware", Impact.Low, Urgency.Low));
        var ticket = await createResponse.Content.ReadFromJsonAsync<TicketResponse>();
        ticket!.Priority.Should().Be(Priority.P4);

        // Update with High Impact and High Urgency
        var updateResponse = await client.PatchAsJsonAsync($"/tickets/{ticket.Id}", new UpdateTicketRequest(
            "Updated Title", "Updated Desc", "Hardware", Impact.High, Urgency.High));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await updateResponse.Content.ReadFromJsonAsync<TicketResponse>();
        updated!.Title.Should().Be("Updated Title");
        updated.Priority.Should().Be(Priority.P1);
    }

    [Fact]
    public async Task Claim_RequesterRole_ReturnsForbidden()
    {
        var requester = await RegisterUserAsync("regular_req");
        using var client = CreateClientWithToken(requester.Token);

        var createResponse = await client.PostAsJsonAsync("/tickets", new CreateTicketRequest("Test", "Desc", "IT", Impact.Low, Urgency.Low));
        var ticket = await createResponse.Content.ReadFromJsonAsync<TicketResponse>();

        // Requester tries to claim -> 403 Forbidden
        var claimResponse = await client.PostAsync($"/tickets/{ticket!.Id}/claim", null);
        claimResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
