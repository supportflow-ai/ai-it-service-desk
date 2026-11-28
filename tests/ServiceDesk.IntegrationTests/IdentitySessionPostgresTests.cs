using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Domain;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class IdentitySessionPostgresTests(TicketIdempotencyPostgresFixture fixture) : IClassFixture<TicketIdempotencyPostgresFixture>
{
    private const string Password = "Test@1234";
    private async Task<UserInfo> UserAsync()
    {
        await using var scope = fixture.First!.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IUserAccountGateway>()
            .CreateRequesterAsync($"session-{Guid.NewGuid():N}@test.invalid", Password, null, default)).User!;
    }
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static async Task<AuthResult> LoginAsync(HttpClient client, UserInfo user)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResult>())!;
    }

    [Fact]
    public async Task RotationSurvivesHostRecreationAndReplayRevokesOnlyThatFamily()
    {
        var user = await UserAsync();
        using var first = fixture.First!.CreateClient();
        var initial = await LoginAsync(first, user);
        var otherDevice = await LoginAsync(first, user);
        await using var recreated = new TicketIdempotencyPostgresFixture.Factory(fixture.ConnectionString);
        using var afterRestart = recreated.CreateClient();
        using var rotated = await afterRestart.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = initial.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var next = (await rotated.Content.ReadFromJsonAsync<AuthResult>())!;
        using var second = fixture.Second!.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = initial.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await afterRestart.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = next.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = otherDevice.RefreshToken })).StatusCode);
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.RefreshTokens.CountAsync(t => t.UserId == user.UserId && t.RevokedAt == null));
    }

    [Fact]
    public async Task ConcurrentRefreshAndLogoutAcrossHostsLeaveTheFamilyRevoked()
    {
        var user = await UserAsync();
        using var first = fixture.First!.CreateClient();
        using var second = fixture.Second!.CreateClient();
        var initial = await LoginAsync(first, user);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> SendAsync(HttpClient client, string route)
        {
            await start.Task;
            return await client.PostAsJsonAsync(route, new { refreshToken = initial.RefreshToken });
        }
        var refresh = SendAsync(first, "/api/auth/refresh");
        var revoke = SendAsync(second, "/api/auth/revoke");
        start.SetResult();
        using var refreshed = await refresh;
        using var revoked = await revoke;
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.Contains(refreshed.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized });
        if (refreshed.StatusCode == HttpStatusCode.OK)
        {
            var next = (await refreshed.Content.ReadFromJsonAsync<AuthResult>())!;
            Assert.Equal(HttpStatusCode.Unauthorized, (await second.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = next.RefreshToken })).StatusCode);
        }
        await using var context = fixture.CreateContext();
        Assert.False(await context.RefreshTokens.AnyAsync(t => t.UserId == user.UserId && t.RevokedAt == null));
    }

    [Fact]
    public async Task ExpiredRefreshCannotRotateAndDoesNotRevokeOtherDevices()
    {
        var user = await UserAsync();
        using var first = fixture.First!.CreateClient();
        var other = await LoginAsync(first, user);
        var raw = Guid.NewGuid().ToString();
        var expired = RefreshToken.Issue(user.UserId, Hash(raw), DateTime.UtcNow.Subtract(RefreshToken.Lifetime).AddMinutes(-1));
        await using (var context = fixture.CreateContext())
        {
            context.RefreshTokens.Add(expired);
            await context.SaveChangesAsync();
        }
        using var second = fixture.Second!.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = raw })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await first.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = other.RefreshToken })).StatusCode);
        await using var check = fixture.CreateContext();
        Assert.Single(await check.RefreshTokens.Where(t => t.FamilyId == expired.FamilyId).ToListAsync());
    }
}
