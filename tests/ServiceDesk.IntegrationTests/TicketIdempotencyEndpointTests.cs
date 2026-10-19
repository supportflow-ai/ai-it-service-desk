using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using ServiceDesk.Api.Endpoints;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.CreateTicket;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Idempotency;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Infrastructure.Identity;
using ServiceDesk.Infrastructure.Clock;
using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Api.Identity;
using ServiceDesk.Infrastructure.Options;
using Xunit;

namespace ServiceDesk.IntegrationTests;

// No PostgreSQL/Testcontainers: verifies the real endpoint + JWT authorization boundary.
[Trait("Category", "Unit")]
public class TicketIdempotencyEndpointTests
{
    private const string Secret = "only_for_idempotency_unit_tests_01234567890123456789";
    private static readonly CreateTicketCommand Payload = new("Test ticket", "A valid ticket description", "NET");

    private static async Task<WebApplication> CreateAppAsync(ICreateTicket service)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddSingleton(service);
        builder.Services.AddSingleton(Mock.Of<ServiceDesk.Application.Ticketing.GetMyTickets.IGetMyTickets>());
        builder.Services.AddSingleton(Mock.Of<ServiceDesk.Application.Ticketing.GetTicketDetail.IGetTicketDetail>());
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = "ServiceDesk",
                ValidateAudience = true, ValidAudience = "ServiceDeskClient",
                ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                RoleClaimType = System.Security.Claims.ClaimTypes.Role
            };
        });
        builder.Services.AddAuthorization(options => options.AddPolicy(
            PolicyNames.RequireRequester, policy => policy.RequireRole(RoleNames.Requester, RoleNames.Agent, RoleNames.Admin)));
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapTicketEndpoints();
        await app.StartAsync();
        return app;
    }

    private static string Token(Guid requester, string role = RoleNames.Requester, int minutes = 60) =>
        new JwtTokenGenerator(Options.Create(new JwtOptions
        {
            SecretKey = Secret, Issuer = "ServiceDesk", Audience = "ServiceDeskClient", ExpiryMinutes = minutes
        }), new SystemClock()).GenerateToken(new UserInfo { UserId = requester.ToString(), Email = "test@example.com", Roles = [role] });

    [Theory]
    [InlineData("missing")]
    [InlineData("invalid")]
    [InlineData("expired")]
    public async Task T09_UnauthorizedCannotReachReplay(string mode)
    {
        var service = new Mock<ICreateTicket>(MockBehavior.Strict);
        await using var app = await CreateAppAsync(service.Object);
        using var client = app.GetTestClient();
        if (mode != "missing")
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", mode == "invalid" ? "invalid-token" : Token(Guid.NewGuid(), minutes: -60));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/tickets");
        request.Headers.Add("Idempotency-Key", "previously-used-key");
        request.Content = JsonContent.Create(Payload);
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task KeyedRequestPassesJwtRequesterIdentity_AndOriginal201Contract()
    {
        var requester = Guid.NewGuid();
        var dto = new TicketDto(Guid.NewGuid(), "IT-2026-0001", Payload.Title, Payload.Description, "NET", 1, DateTime.UtcNow);
        var service = new Mock<ICreateTicket>(MockBehavior.Strict);
        service.Setup(s => s.ExecuteAsync(Payload, "key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        await using var app = await CreateAppAsync(service.Object);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(requester));
        client.DefaultRequestHeaders.Add("Idempotency-Key", "key");
        using var response = await client.PostAsJsonAsync("/api/tickets", Payload);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().Be($"/api/tickets/{dto.Id}");
        (await response.Content.ReadFromJsonAsync<TicketDto>()).Should().Be(dto);
        service.VerifyAll();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PayloadConflictAndLockTimeout_HaveExplicitHttpContracts(bool inProgress)
    {
        var requester = Guid.NewGuid();
        var service = new Mock<ICreateTicket>(MockBehavior.Strict);
        service.Setup(s => s.ExecuteAsync(Payload, "key", It.IsAny<CancellationToken>()))
            .ThrowsAsync(inProgress
                ? new IdempotencyInProgressException(new Exception("test"))
                : new IdempotencyConflictException());
        await using var app = await CreateAppAsync(service.Object);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(requester));
        client.DefaultRequestHeaders.Add("Idempotency-Key", "key");
        using var response = await client.PostAsJsonAsync("/api/tickets", Payload);
        response.StatusCode.Should().Be(inProgress ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Conflict);
        if (inProgress) response.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(1));
        service.VerifyAll();
    }

    [Fact]
    public async Task RepeatedHeaderValues_AreRejectedBeforeService()
    {
        var service = new Mock<ICreateTicket>(MockBehavior.Strict);
        await using var app = await CreateAppAsync(service.Object);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(Guid.NewGuid()));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/tickets");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", new[] { "first", "second" });
        request.Content = JsonContent.Create(Payload);
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AuthenticatedUserWithoutAllowedRole_CannotReachReplay()
    {
        var service = new Mock<ICreateTicket>(MockBehavior.Strict);
        await using var app = await CreateAppAsync(service.Object);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(Guid.NewGuid(), "Observer"));
        client.DefaultRequestHeaders.Add("Idempotency-Key", "key");
        using var response = await client.PostAsJsonAsync("/api/tickets", Payload);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        service.VerifyNoOtherCalls();
    }
}
