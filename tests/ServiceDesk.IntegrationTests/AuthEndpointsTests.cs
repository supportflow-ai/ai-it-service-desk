using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using ServiceDesk.Application.Auth;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public class AuthEndpointsTests : IClassFixture<ServiceDeskWebApplicationFactory>
{
    private readonly ServiceDeskWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointsTests(ServiceDeskWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_Login_AndMe_ReturnJwtPrincipal()
    {
        var email = $"requester_{Guid.NewGuid():N}@example.com";
        var password = "ValidPassword123!";

        var registered = await _client.PostAsJsonAsync("/auth/register", new RegisterRequest(email, password));
        registered.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await registered.Content.ReadFromJsonAsync<AuthResult>();
        auth.Should().NotBeNull();
        auth!.Token.Should().NotBeNullOrWhiteSpace();
        auth.User.Email.Should().Be(email);
        auth.User.Roles.Should().Contain(ApplicationRoles.Requester);

        // Login with same credentials
        var loggedIn = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password));
        loggedIn.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginAuth = await loggedIn.Content.ReadFromJsonAsync<AuthResult>();
        loginAuth.Should().NotBeNull();
        loginAuth!.Token.Should().NotBeNullOrWhiteSpace();

        // Call /auth/me with Bearer token
        using var authedClient = _factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        var meResponse = await authedClient.GetAsync("/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meUser = await meResponse.Content.ReadFromJsonAsync<AuthUser>();
        meUser.Should().NotBeNull();
        meUser!.Email.Should().Be(email);
        meUser.Roles.Should().Contain(ApplicationRoles.Requester);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsForbidden()
    {
        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("nonexistent@example.com", "WrongPassword123!"));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Register_MissingEmailOrPassword_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/auth/register", new RegisterRequest("", "Password123!"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMe_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangeRole_Anonymous_ReturnsUnauthorized()
    {
        var response = await _client.PutAsJsonAsync($"/admin/users/{Guid.NewGuid()}/role", new ChangeRoleRequest(ApplicationRoles.Agent));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangeRole_Requester_ReturnsForbidden()
    {
        var email = $"requester_{Guid.NewGuid():N}@example.com";
        var password = "ValidPassword123!";

        var registered = await _client.PostAsJsonAsync("/auth/register", new RegisterRequest(email, password));
        var auth = await registered.Content.ReadFromJsonAsync<AuthResult>();

        using var requesterClient = _factory.CreateClient();
        requesterClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var response = await requesterClient.PutAsJsonAsync($"/admin/users/{auth.User.Id}/role", new ChangeRoleRequest(ApplicationRoles.Agent));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangeRole_Admin_CanPromoteRequesterToAgent()
    {
        // 1. Register a user as Requester
        var userEmail = $"user_{Guid.NewGuid():N}@example.com";
        var userPassword = "UserPass123!";
        var registered = await _client.PostAsJsonAsync("/auth/register", new RegisterRequest(userEmail, userPassword));
        var userAuth = await registered.Content.ReadFromJsonAsync<AuthResult>();

        // 2. Log in as seeded Admin
        var adminLogin = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin@servicedesk.local", "AdminPass123!"));
        adminLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminAuth = await adminLogin.Content.ReadFromJsonAsync<AuthResult>();
        adminAuth!.User.Roles.Should().Contain(ApplicationRoles.Admin);

        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.Token);

        // 3. Admin promotes user to Agent
        var changeResponse = await adminClient.PutAsJsonAsync($"/admin/users/{userAuth!.User.Id}/role", new ChangeRoleRequest(ApplicationRoles.Agent));
        changeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 4. Verify user has Agent role
        using var userClient = _factory.CreateClient();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userAuth.Token);

        var meResponse = await userClient.GetAsync("/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var meUser = await meResponse.Content.ReadFromJsonAsync<AuthUser>();
        meUser!.Roles.Should().Contain(ApplicationRoles.Agent);
    }

    [Fact]
    public async Task ChangeRole_InvalidRole_ReturnsBadRequest()
    {
        var adminLogin = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin@servicedesk.local", "AdminPass123!"));
        var adminAuth = await adminLogin.Content.ReadFromJsonAsync<AuthResult>();

        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.Token);

        var response = await adminClient.PutAsJsonAsync($"/admin/users/{Guid.NewGuid()}/role", new ChangeRoleRequest("SuperUser"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangeRole_NonExistentUser_ReturnsNotFound()
    {
        var adminLogin = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin@servicedesk.local", "AdminPass123!"));
        var adminAuth = await adminLogin.Content.ReadFromJsonAsync<AuthResult>();

        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.Token);

        var response = await adminClient.PutAsJsonAsync($"/admin/users/{Guid.NewGuid()}/role", new ChangeRoleRequest(ApplicationRoles.Agent));
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
