using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using ServiceDesk.Application.Ticketing.Commands;
using ServiceDesk.Application.Ticketing.Dtos;
using ServiceDesk.Domain.Ticketing;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public class TicketEndpointTests : IClassFixture<ServiceDeskWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TicketEndpointTests(ServiceDeskWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateTicket_ValidPayload_Returns201()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var command = new CreateTicketCommand(
            Title: "Network access issue",
            Description: "Cannot connect to VPN from home",
            CategoryId: TicketCategories.NET
        );

        var response = await _client.PostAsJsonAsync("/api/tickets", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var ticket = await response.Content.ReadFromJsonAsync<TicketDto>();
        ticket.Should().NotBeNull();
        ticket!.Id.Should().NotBeEmpty();
        ticket.TicketNumber.Should().MatchRegex(@"^IT-\d{4}-\d{4}$");
        ticket.Title.Should().Be("Network access issue");
        ticket.Description.Should().Be("Cannot connect to VPN from home");
        ticket.CategoryId.Should().Be(TicketCategories.NET);
        response.Headers.Location!.ToString().Should().Be($"/api/tickets/{ticket.Id}");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task CreateTicket_WithoutToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var command = new CreateTicketCommand(
            Title: "Network access issue",
            Description: "Cannot connect to VPN from home",
            CategoryId: TicketCategories.NET
        );

        var response = await _client.PostAsJsonAsync("/api/tickets", command);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateTicket_EmptyTitle_Returns400()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var command = new CreateTicketCommand(
            Title: "",
            Description: "Cannot connect to VPN from home",
            CategoryId: TicketCategories.NET
        );

        var response = await _client.PostAsJsonAsync("/api/tickets", command);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task CreateTicket_ShortDescription_Returns400()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var command = new CreateTicketCommand(
            Title: "Network access issue",
            Description: "Short",
            CategoryId: TicketCategories.NET
        );

        var response = await _client.PostAsJsonAsync("/api/tickets", command);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task CreateTicket_InvalidCategory_Returns400()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var command = new CreateTicketCommand(
            Title: "Network access issue",
            Description: "Cannot connect to VPN from home",
            CategoryId: "UNKNOWN"
        );

        var response = await _client.PostAsJsonAsync("/api/tickets", command);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = $"ticket_user_{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Test@1234",
            fullName = "Ticket Test User"
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token!;
    }

    private sealed record AuthResponse(string? Token, object? User);
}
