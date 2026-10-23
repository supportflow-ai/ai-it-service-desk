using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Moq;
using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Errors;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Ticketing;
using ServiceDesk.Infrastructure.Clock;
using ServiceDesk.Infrastructure.Identity;
using ServiceDesk.Infrastructure.Options;
using Xunit;

namespace ServiceDesk.IntegrationTests;

// HTTP, JWT, use case và bộ lọc thật; chỉ thay cổng ghi để kiểm tra quyền/thông điệp độc lập với PostgreSQL.
[Trait("Category", "Unit")]
public sealed class TicketLifecycleBoundaryTests
{
    private const string Secret = "lifecycle_boundary_secret_01234567890123456789";

    [Theory]
    [InlineData(RoleNames.Agent, HttpStatusCode.OK)]
    [InlineData(RoleNames.Admin, HttpStatusCode.OK)]
    [InlineData(RoleNames.Requester, HttpStatusCode.Forbidden)]
    [InlineData("Requester,Admin", HttpStatusCode.OK)]
    public async Task Claim_HttpAndApplicationAgreeOnStaffRoles(string roles, HttpStatusCode expected)
    {
        var actor = Guid.NewGuid();
        var ticket = CreateTicket(actor);
        var writer = new Mock<ITicketWriter>(MockBehavior.Strict);
        if (expected == HttpStatusCode.OK)
        {
            writer.Setup(value => value.GetForUpdateAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
            writer.Setup(value => value.UpdateTicketAsync(ticket, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => ToDto(ticket));
        }
        await using var factory = new Factory(writer.Object);
        using var client = Client(factory, actor, roles);
        using var response = await client.PostAsync($"/api/tickets/{ticket.Id}/claim", null);

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<TicketDto>();
            Assert.Equal((int)TicketStatus.InProgress, result!.Status);
            writer.VerifyAll();
        }
        writer.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(RoleNames.Requester)]
    [InlineData("Observer")]
    public async Task Resolve_ForbiddenRoleCannotDistinguishExistingAndMissingTicket(string role)
    {
        var writer = new Mock<ITicketWriter>(MockBehavior.Strict);
        await using var factory = new Factory(writer.Object);
        var actor = Guid.NewGuid();
        var existing = CreateTicket(Guid.NewGuid());
        // Nếu authorization gọi persistence, mock strict sẽ làm phép thử thất bại.
        using var client = Client(factory, actor, role);
        using var first = await client.PostAsync($"/api/tickets/{existing.Id}/resolve", null);
        using var second = await client.PostAsync($"/api/tickets/{Guid.NewGuid()}/resolve", null);

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(first.StatusCode, second.StatusCode);
        // Trace/instance của ProblemDetails có thể khác giữa request, nhưng không có dữ liệu phiếu.
        foreach (var response in new[] { first, second })
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(existing.Title, body, StringComparison.Ordinal);
            Assert.DoesNotContain(existing.Description, body, StringComparison.Ordinal);
        }
        writer.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("update")]
    [InlineData("claim")]
    [InlineData("resolve")]
    [InlineData("reopen")]
    [InlineData("close")]
    [InlineData("cancel")]
    public async Task ConcurrentWrite_Returns409WithLegacyJsonAndNoTechnicalDetails(string action)
    {
        var actor = Guid.NewGuid();
        var ticket = CreateTicket(actor);
        PrepareState(ticket, action);
        var writer = new Mock<ITicketWriter>(MockBehavior.Strict);
        writer.Setup(value => value.GetForUpdateAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        var conflict = new TicketConcurrencyException(new Exception("private persistence details"));
        writer.Setup(value => value.UpdateTicketAsync(ticket, It.IsAny<CancellationToken>())).ThrowsAsync(conflict);
        await using var factory = new Factory(writer.Object);
        using var client = Client(factory, actor, RoleNames.Admin);
        using var response = action == "update"
            ? await client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/update",
                new { title = "Updated title", description = "Description after update", categoryId = "NET" })
            : await client.PostAsync($"/api/tickets/{ticket.Id}/{action}", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = Assert.Single(json.RootElement.EnumerateObject());
        Assert.Equal("error", error.Name);
        Assert.Equal(conflict.Message, error.Value.GetString());
        Assert.Null(response.Headers.RetryAfter);
        writer.Verify(value => value.UpdateTicketAsync(ticket, It.IsAny<CancellationToken>()), Times.Once);
        writer.VerifyAll();
        writer.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("update")]
    [InlineData("claim")]
    [InlineData("resolve")]
    [InlineData("reopen")]
    [InlineData("close")]
    [InlineData("cancel")]
    public async Task AnonymousLifecycleRequest_NeverReachesPersistence(string action)
    {
        var writer = new Mock<ITicketWriter>(MockBehavior.Strict);
        await using var factory = new Factory(writer.Object);
        using var client = factory.CreateClient();
        using var response = action == "update"
            ? await client.PostAsJsonAsync($"/api/tickets/{Guid.NewGuid()}/update",
                new { title = "Updated title", description = "Description after update", categoryId = "NET" })
            : await client.PostAsync($"/api/tickets/{Guid.NewGuid()}/{action}", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        writer.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("update")]
    [InlineData("reopen")]
    [InlineData("close")]
    [InlineData("cancel")]
    public async Task NonOwnerNeverReachesTheConflictOrWriteStage(string action)
    {
        var ticket = CreateTicket(Guid.NewGuid());
        PrepareState(ticket, action);
        var writer = new Mock<ITicketWriter>(MockBehavior.Strict);
        writer.Setup(value => value.GetForUpdateAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        await using var factory = new Factory(writer.Object);
        using var client = Client(factory, Guid.NewGuid(), RoleNames.Requester);
        using var response = action == "update"
            ? await client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/update",
                new { title = "Updated title", description = "Description after update", categoryId = "NET" })
            : await client.PostAsync($"/api/tickets/{ticket.Id}/{action}", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        writer.VerifyAll();
        writer.VerifyNoOtherCalls();
    }

    private static Ticket CreateTicket(Guid requester) =>
        Ticket.Create(requester, "Network issue", "Description long enough", "NET", DateTime.UtcNow);

    internal static void PrepareState(Ticket ticket, string action)
    {
        if (action is "resolve" or "reopen" or "close") ticket.Claim(DateTime.UtcNow);
        if (action is "reopen" or "close") ticket.Resolve(DateTime.UtcNow);
    }

    private static TicketDto ToDto(Ticket ticket) => new(ticket.Id, "IT-2026-0001", ticket.Title,
        ticket.Description, ticket.CategoryId, (int)ticket.Status, ticket.CreatedAt);

    private static HttpClient Client(Factory factory, Guid actor, string roles)
    {
        var client = factory.CreateClient();
        var token = new JwtTokenGenerator(Options.Create(new JwtOptions
        {
            SecretKey = Secret, Issuer = "ServiceDesk", Audience = "ServiceDeskClient", ExpiryMinutes = 60
        }), new SystemClock()).GenerateToken(new UserInfo
        {
            UserId = actor.ToString(), Email = "lifecycle@test.invalid", Roles = roles.Split(',')
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private sealed class Factory(ITicketWriter writer) : WebApplicationFactory<ServiceDesk.Api.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:SecretKey"] = Secret,
                    ["Jwt:Issuer"] = "ServiceDesk",
                    ["Jwt:Audience"] = "ServiceDeskClient"
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITicketWriter>();
                services.AddSingleton(writer);
            });
        }
    }
}
