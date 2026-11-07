using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class TicketCommentAccessTests(TicketIdempotencyPostgresFixture fixture)
    : IClassFixture<TicketIdempotencyPostgresFixture>
{
    [Fact]
    public async Task Requester_CannotCommentOnAnotherRequestersTicket()
    {
        using var owner = fixture.First!.CreateClient();
        var ownerIdentity = await RegisterAsync(owner);
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerIdentity.Token);
        var ticket = await CreateTicketAsync(owner);

        using var requester = fixture.Second!.CreateClient();
        var requesterIdentity = await RegisterAsync(requester);
        requester.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterIdentity.Token);

        using var response = await requester.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new { content = "Unauthorized requester comment" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("Agent")]
    [InlineData("Admin")]
    public async Task Staff_CanCommentOnAnotherRequestersTicket(string role)
    {
        using var owner = fixture.First!.CreateClient();
        var ownerIdentity = await RegisterAsync(owner);
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerIdentity.Token);
        var ticket = await CreateTicketAsync(owner);

        using var staff = fixture.Second!.CreateClient();
        var staffIdentity = await RegisterAsync(staff);
        await PromoteAsync(staffIdentity.User.UserId, role);
        staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await LoginAsync(staff, staffIdentity.User.Email)).Token);

        using var response = await staff.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new { content = "Staff comment" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<TicketDto> CreateTicketAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = "Comment access verification",
            description = "Ticket for comment ownership verification.",
            categoryId = "NET"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TicketDto>())!;
    }

    private static async Task<AuthResponse> RegisterAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"comment-access-{Guid.NewGuid():N}@test.invalid",
            password = "Test@1234"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private async Task PromoteAsync(string userId, string role)
    {
        await using var scope = fixture.Second!.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(userId))!, role)).Succeeded);
    }

    private static async Task<AuthResponse> LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test@1234" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private sealed record AuthResponse(string Token, UserInfo User);
}
