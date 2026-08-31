using System.Net;
using FluentAssertions;
using Xunit;

namespace ServiceDesk.IntegrationTests;

/// <summary>
/// Integration tests for the API health check and startup.
/// Uses real PostgreSQL via Testcontainers.
/// </summary>
[Trait("Category", "Integration")]
public class HealthCheckTests : IClassFixture<ServiceDeskWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthCheckTests(ServiceDeskWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task ApiInfo_ReturnsOk()
    {
        var response = await _client.GetAsync("/api");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("AI IT Service Desk API");
    }

    [Fact]
    public async Task Swagger_ReturnsOk_InDevelopment()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
