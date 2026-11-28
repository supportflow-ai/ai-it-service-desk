using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Npgsql;
using ServiceDesk.Platform.Persistence;
using Microsoft.IdentityModel.Tokens;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Common;
using ServiceDesk.Modules.Identity.Domain;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Infrastructure;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class JwtValidationPostgresTests(TicketIdempotencyPostgresFixture fixture) : IClassFixture<TicketIdempotencyPostgresFixture>
{
    private const string Password = "Test@1234";

    private async Task<UserInfo> CreateUserAsync()
    {
        await using var scope = fixture.First!.Services.CreateAsyncScope();
        var created = await scope.ServiceProvider.GetRequiredService<IUserAccountGateway>()
            .CreateRequesterAsync($"jwt-{Guid.NewGuid():N}@test.invalid", Password, null, default);
        Assert.NotNull(created.User);
        Assert.False(string.IsNullOrWhiteSpace(created.User.SecurityStamp));
        return created.User;
    }

    private static string Token(UserInfo user, string mode = "valid")
    {
        var claims = new List<Claim>();
        if (mode != "missing-subject") claims.Add(new Claim(ClaimTypes.NameIdentifier, user.UserId));
        if (mode != "missing-stamp") claims.Add(new Claim(JwtTokenGenerator.SecurityStampClaimType,
            mode == "wrong-stamp" ? "wrong-stamp" : mode == "blank-stamp" ? " " : user.SecurityStamp!));
        var token = new JwtSecurityToken(
            mode == "issuer" ? "wrong-issuer" : "ServiceDesk",
            mode == "audience" ? "wrong-audience" : "ServiceDeskClient", claims,
            expires: DateTime.UtcNow.AddHours(mode == "expired" ? -1 : 1),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                mode == "signature" ? new string('x', 64) : TicketIdempotencyPostgresFixture.Secret)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task AssertBothHostsAsync(string token, HttpStatusCode expected)
    {
        foreach (var host in new[] { fixture.First!, fixture.Second! })
        {
            using var client = host.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await client.GetAsync("/api/auth/me");
            Assert.Equal(expected, response.StatusCode);
            if (expected == HttpStatusCode.Unauthorized)
                Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).ToString());
        }
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("missing-subject")]
    [InlineData("missing-stamp")]
    [InlineData("blank-stamp")]
    [InlineData("wrong-stamp")]
    public async Task InvalidTokenCannotAuthenticateOnEitherHost(string mode)
    {
        var user = await CreateUserAsync();
        await AssertBothHostsAsync(Token(user), HttpStatusCode.OK);
        await AssertBothHostsAsync(Token(user, mode), HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("missing-stamp")]
    public async Task DeletedUserIsRejectedEvenWithASignedToken(string mode)
    {
        var user = await CreateUserAsync();
        var token = Token(user, mode);
        await using (var context = fixture.CreateContext())
            await context.Users.Where(u => u.Id == user.UserId).ExecuteDeleteAsync();
        await AssertBothHostsAsync(token, HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task MissingStoredStampCannotAuthenticate(string? stamp)
    {
        var user = await CreateUserAsync();
        await using (var context = fixture.CreateContext())
            await context.Users.Where(u => u.Id == user.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.SecurityStamp, stamp));
        await AssertBothHostsAsync(Token(user), HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("reset")]
    [InlineData("roles")]
    [InlineData("revoke")]
    public async Task SecurityChangeRevokesOldAccessTokenAcrossHosts(string change)
    {
        var user = await CreateUserAsync();
        using var client = fixture.First!.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<AuthResult>())!;
        await AssertBothHostsAsync(auth.Token!, HttpStatusCode.OK);
        await using (var scope = fixture.First.Services.CreateAsyncScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IUserAccountGateway>();
            if (change == "password")
                Assert.True((await accounts.ChangePasswordAsync(user.UserId, Password, "Changed@1234", DateTime.UtcNow, default)).Succeeded);
            else if (change == "reset")
            {
                var hash = TokenHasher.Hash("112233");
                var now = DateTime.UtcNow;
                Assert.True(await scope.ServiceProvider.GetRequiredService<IOtpCodeStore>()
                    .TryIssueAsync(OtpCode.Issue(user.Email, OtpCode.ResetPasswordPurpose, hash, now), 5, now.AddHours(-1)));
                Assert.True((await scope.ServiceProvider.GetRequiredService<IPasswordResetGateway>()
                    .ResetWithOtpAsync(user.Email, hash, "Changed@1234", now)).Succeeded);
            }
            else if (change == "roles")
                Assert.True((await accounts.AssignRolesAsync(user.UserId, [RoleNames.Agent], DateTime.UtcNow, default)).Succeeded);
            else
                Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/revoke", new { refreshToken = auth.RefreshToken })).StatusCode);
        }
        await AssertBothHostsAsync(auth.Token!, HttpStatusCode.Unauthorized);
    }

[Fact]
    public async Task UnavailableDatabaseReturnsGenericServerError()
    {
        var user = await CreateUserAsync();
        var unavailable = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Host = "127.0.0.1", Port = 1, Timeout = 1
        };
        await using var host = fixture.First!.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContext>();
            services.AddScoped<DbContext>(_ => new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(unavailable.ConnectionString).Options));
        }));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(user));

        using var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Npgsql", body);
        Assert.DoesNotContain("127.0.0.1", body);
        Assert.DoesNotContain("Connection", body);
        Assert.Null(response.Headers.WwwAuthenticate.FirstOrDefault());
    }

    [Fact]
    public async Task RegisterLoginAndRefreshIssueStampedTokensAcceptedAcrossHosts()
    {
        using var client = fixture.First!.CreateClient();
        var email = $"jwt-register-{Guid.NewGuid():N}@test.invalid";
        using var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var registered = (await register.Content.ReadFromJsonAsync<AuthResult>())!;
        await AssertBothHostsAsync(registered.Token!, HttpStatusCode.OK);
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loggedIn = (await login.Content.ReadFromJsonAsync<AuthResult>())!;
        using var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = loggedIn.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var refreshed = (await refresh.Content.ReadFromJsonAsync<AuthResult>())!;
        foreach (var token in new[] { registered.Token!, loggedIn.Token!, refreshed.Token! })
        {
            Assert.Contains(new JwtSecurityTokenHandler().ReadJwtToken(token).Claims,
                c => c.Type == JwtTokenGenerator.SecurityStampClaimType && !string.IsNullOrWhiteSpace(c.Value));
            await AssertBothHostsAsync(token, HttpStatusCode.OK);
        }
    }
}
