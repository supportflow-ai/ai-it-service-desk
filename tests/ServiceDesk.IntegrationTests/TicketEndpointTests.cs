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

    [Fact]
    public async Task CreateTicket_WithIdempotencyKey_ReturnsSameTicketOnDuplicateRequest()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var idempotencyKey = Guid.NewGuid().ToString();
        var command = new CreateTicketCommand(
            Title: "Idempotent ticket",
            Description: "Testing idempotency key caching",
            CategoryId: TicketCategories.NET
        );

        using var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/tickets")
        {
            Content = JsonContent.Create(command)
        };
        request1.Headers.Add("Idempotency-Key", idempotencyKey);

        var response1 = await _client.SendAsync(request1);
        response1.StatusCode.Should().Be(HttpStatusCode.Created);
        var ticket1 = await response1.Content.ReadFromJsonAsync<TicketDto>();
        ticket1.Should().NotBeNull();

        using var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/tickets")
        {
            Content = JsonContent.Create(command)
        };
        request2.Headers.Add("Idempotency-Key", idempotencyKey);

        var response2 = await _client.SendAsync(request2);
        response2.StatusCode.Should().Be(HttpStatusCode.Created);
        var ticket2 = await response2.Content.ReadFromJsonAsync<TicketDto>();
        ticket2.Should().NotBeNull();

        ticket2!.Id.Should().Be(ticket1!.Id);
        ticket2.TicketNumber.Should().Be(ticket1.TicketNumber);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetTicketById_ExistingTicket_Returns200WithTicketDto()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var command = new CreateTicketCommand(
            Title: "Get ticket test",
            Description: "Testing get ticket by id endpoint",
            CategoryId: TicketCategories.ACC
        );

        var createResponse = await _client.PostAsJsonAsync("/api/tickets", command);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdTicket = await createResponse.Content.ReadFromJsonAsync<TicketDto>();
        createdTicket.Should().NotBeNull();

        var getResponse = await _client.GetAsync($"/api/tickets/{createdTicket!.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetchedTicket = await getResponse.Content.ReadFromJsonAsync<TicketDto>();
        fetchedTicket.Should().NotBeNull();
        fetchedTicket!.Id.Should().Be(createdTicket.Id);
        fetchedTicket.TicketNumber.Should().Be(createdTicket.TicketNumber);
        fetchedTicket.Title.Should().Be("Get ticket test");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetTicketById_NonExistentTicket_Returns404()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync($"/api/tickets/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetTicketById_WithoutToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync($"/api/tickets/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
