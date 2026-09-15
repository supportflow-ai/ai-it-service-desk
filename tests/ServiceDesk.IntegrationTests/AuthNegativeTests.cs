using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public class AuthNegativeTests : IClassFixture<ServiceDeskWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthNegativeTests(ServiceDeskWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProtectedEndpoint_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/api/diag/protected");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_InvalidToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "garbage_token_string");

        var response = await _client.GetAsync("/api/diag/protected");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task ProtectedEndpoint_ExpiredToken_Returns401()
    {
        var expiredToken = GenerateExpiredToken();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", expiredToken);

        var response = await _client.GetAsync("/api/diag/protected");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task AdminEndpoint_AsRequester_Returns403()
    {
        var token = await RegisterAndGetToken("admin_req_");

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/diag/admin-only");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task Register_RequestingAdminRole_IsEnforcedAsRequester_CannotAccessAdmin()
    {
        var email = $"escalation_{Guid.NewGuid():N}@test.com";
        var regResp = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Test@1234",
            role = "Admin"
        });

        regResp.EnsureSuccessStatusCode();
        var body = await regResp.Content.ReadFromJsonAsync<AuthResponse>();
        var token = body!.Token!;

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/diag/admin-only");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task AdminEndpoint_AsAgent_Returns403()
    {
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "agent@test.com",
            password = "Test@1234"
        });

        loginResp.EnsureSuccessStatusCode();
        var body = await loginResp.Content.ReadFromJsonAsync<AuthResponse>();
        var token = body!.Token!;

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/diag/admin-only");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task AgentEndpoint_AsRequester_Returns403()
    {
        var token = await RegisterAndGetToken("agent_req_");

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/diag/agent-only");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    private async Task<string> RegisterAndGetToken(string prefix)
    {
        var email = $"{prefix}{Guid.NewGuid():N}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Test@1234"
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token!;
    }

    private static string GenerateExpiredToken()
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("development_secret_key_minimum_32_characters_long_test"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "ServiceDesk",
            audience: "ServiceDeskClient",
            claims: [new Claim(ClaimTypes.NameIdentifier, "test-user")],
            expires: DateTime.UtcNow.AddMinutes(-10),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record AuthResponse(string? Token, object? User);
}
