using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.CreateTicket;
using ServiceDesk.Domain.Ticketing;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class TicketCommentAuthorizationEndpointTests(ServiceDeskWebApplicationFactory factory)
    : IClassFixture<ServiceDeskWebApplicationFactory>
{
    [Fact]
    public async Task OtherRequester_CannotComment_AndOwnerTimelineRemainsUnchanged()
    {
        using var owner = await RegisterAsync();
        using var other = await RegisterAsync();
        using var created = await owner.PostAsJsonAsync("/api/tickets",
            new CreateTicketCommand("Lỗi VPN", "Không kết nối được mạng riêng ảo", TicketCategories.VPN));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var ticket = (await created.Content.ReadFromJsonAsync<TicketDto>())!;

        var before = await owner.GetFromJsonAsync<TimelineItemDto[]>($"/api/tickets/{ticket.Id}/timeline");
        using var denied = await other.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new { content = "Không được phép ghi" });
        using var missing = await other.PostAsJsonAsync($"/api/tickets/{Guid.NewGuid()}/comments", new { content = "Không tồn tại" });
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var after = await owner.GetFromJsonAsync<TimelineItemDto[]>($"/api/tickets/{ticket.Id}/timeline");
        Assert.Equal(before, after);

        using var allowed = await owner.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new { content = "Bình luận của chủ sở hữu" });
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        var comment = (await allowed.Content.ReadFromJsonAsync<TimelineItemDto>())!;
        Assert.Equal(ticket.Id, comment.TicketId);
        Assert.False(comment.IsInternal);
        var final = await owner.GetFromJsonAsync<TimelineItemDto[]>($"/api/tickets/{ticket.Id}/timeline");
        Assert.Equal((before?.Length ?? 0) + 1, final!.Length);
        Assert.Contains(final, item => item.Id == comment.Id);
    }

    private async Task<HttpClient> RegisterAsync()
    {
        var client = factory.CreateClient();
        try
        {
            using var response = await client.PostAsJsonAsync("/api/auth/register", new
            {
                email = $"comment_{Guid.NewGuid():N}@example.com",
                password = "Test@1234",
                fullName = "Người dùng kiểm thử bình luận"
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private sealed record AuthResponse(string Token);
}
