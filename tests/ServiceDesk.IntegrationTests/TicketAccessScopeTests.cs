using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Ticketing.Contracts;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class TicketAccessScopeTests(TicketIdempotencyPostgresFixture fixture)
    : IClassFixture<TicketIdempotencyPostgresFixture>
{
    [Theory]
    [InlineData("Requester", HttpStatusCode.NotFound)]
    [InlineData("Agent", HttpStatusCode.OK)]
    [InlineData("Admin", HttpStatusCode.OK)]
    public async Task DetailRetainsLegacyScopeButListAlwaysUsesOwnIdentity(string role, HttpStatusCode expected)
    {
        using var owner = fixture.First!.CreateClient();
        var ownerIdentity = await RegisterAsync(owner);
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerIdentity.Token);
        using var created = await owner.PostAsJsonAsync("/api/tickets", new
        { title = "Scope verification", description = "Another requester's private ticket", categoryId = "NET" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var ticket = (await created.Content.ReadFromJsonAsync<TicketDto>())!;

        using var actor = fixture.Second!.CreateClient();
        var identity = await RegisterAsync(actor);
        if (role != "Requester")
        {
            await using var scope = fixture.Second.Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(identity.User.UserId))!, role)).Succeeded);
            using var login = await actor.PostAsJsonAsync("/api/auth/login", new { email = identity.User.Email, password = "Test@1234" });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            identity = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        }
        actor.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", identity.Token);
        using var detail = await actor.GetAsync($"/api/tickets/{ticket.Id}");
        Assert.Equal(expected, detail.StatusCode);
        var list = await actor.GetFromJsonAsync<PagedTicketResult>("/api/tickets/me?page=1&pageSize=10");
        Assert.Equal(0, list!.TotalCount);
        Assert.Empty(list.Items);
    }

    private static async Task<AuthResponse> RegisterAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = $"scope-{Guid.NewGuid()}@test.invalid", password = "Test@1234" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private sealed record AuthResponse(string Token, UserInfo User);
}
