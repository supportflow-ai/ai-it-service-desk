using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ServiceDesk.Modules.Ticketing.Presentation;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.CreateTicket;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Idempotency;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Identity;
using ServiceDesk.Api.Authentication;
using ServiceDesk.Modules.Identity.Infrastructure.Options;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public class TicketIdempotencyEndpointTests(TicketIdempotencyPostgresFixture fixture) : IClassFixture<TicketIdempotencyPostgresFixture>
{
    private static readonly CreateTicketCommand Payload = new("Test ticket", "A valid ticket description", "NET");

    private async Task<WebApplication> CreateAppAsync(ICreateTicket service)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
        builder.Services.AddSingleton(service);
        builder.Services.AddSingleton(Mock.Of<ServiceDesk.Modules.Ticketing.Application.GetMyTickets.IGetMyTickets>());
        builder.Services.AddSingleton(Mock.Of<ServiceDesk.Modules.Ticketing.Application.GetTicketDetail.IGetTicketDetail>());
        builder.Services.AddSingleton(Mock.Of<ServiceDesk.Modules.Ticketing.Application.UpdateTicket.IUpdateTicket>());
        builder.Services.AddSingleton(Mock.Of<ServiceDesk.Modules.Ticketing.Application.ClaimTicket.IClaimTicket>());
        builder.Services.AddSingleton(Mock.Of<ServiceDesk.Modules.Ticketing.Application.PendingTicket.IPendingTicket>());
        builder.Services.AddSingleton(Mock.Of<ServiceDesk.Modules.Ticketing.Application.ResumeTicket.IResumeTicket>());
        builder.Services.AddSingleton(Mock.Of<ServiceDesk.Modules.Ticketing.Application.ResolveTicket.IResolveTicket>());
        builder.Services.AddSingleton(Mock.Of<ServiceDesk.Modules.Ticketing.Application.ReopenTicket.IReopenTicket>());
        builder.Services.AddSingleton(Mock.Of<ServiceDesk.Modules.Ticketing.Application.CloseTicket.ICloseTicket>());
        builder.Services.AddSingleton(Mock.Of<ServiceDesk.Modules.Ticketing.Application.CancelTicket.ICancelTicket>());
        builder.Services.Configure<JwtOptions>(options =>
        {
            options.SecretKey = TicketIdempotencyPostgresFixture.Secret;
            options.Issuer = "ServiceDesk";
            options.Audience = "ServiceDeskClient";
        });
        builder.Services.AddIdentityAuthentication();
        builder.Services.AddScoped<Microsoft.EntityFrameworkCore.DbContext>(_ => fixture.CreateContext());
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(PolicyNames.RequireRequester, policy => policy.RequireRole(RoleNames.Requester, RoleNames.Agent, RoleNames.Admin));
            options.AddPolicy(PolicyNames.RequireAgent, policy => policy.RequireRole(RoleNames.Agent, RoleNames.Admin));
            options.AddPolicy(PolicyNames.RequireAdmin, policy => policy.RequireRole(RoleNames.Admin));
        });
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapTicketEndpoints();
        await app.StartAsync();
        return app;
    }

    private Task<string> TokenAsync(Guid requester, string role = RoleNames.Requester, int minutes = 60) =>
        fixture.CreateTokenAsync(requester, role, minutes);

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
                "Bearer", mode == "invalid" ? "invalid-token" : await TokenAsync(Guid.NewGuid(), minutes: -60));
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
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(requester));
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
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(requester));
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
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(Guid.NewGuid()));
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
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(Guid.NewGuid(), "Observer"));
        client.DefaultRequestHeaders.Add("Idempotency-Key", "key");
        using var response = await client.PostAsJsonAsync("/api/tickets", Payload);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        service.VerifyNoOtherCalls();
    }
}
