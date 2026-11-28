using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.ForgotPassword;
using ServiceDesk.Modules.Identity.Application.RefreshAuth;
using ServiceDesk.Modules.Identity.Presentation;
using Xunit;

namespace ServiceDesk.IntegrationTests;

/// <summary>
/// Kiểm thử offline cho giới hạn tần suất của endpoint xác thực: vượt ngưỡng theo IP trả 429,
/// request không có địa chỉ IP thì không bị giới hạn.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AuthRateLimitingTests
{
    private static readonly ForgotPasswordRequest Request = new("rate-limit@test.invalid");

    [Fact]
    public async Task RequestsBeyondTheLimitFromOneIpAreRejectedWith429()
    {
        await using var factory = new RateLimitFactory(remoteIp: IPAddress.Parse("203.0.113.10"));
        using var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
            statuses.Add((await client.PostAsJsonAsync("/api/auth/forgot-password", Request)).StatusCode);

        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests }, statuses);
    }

    [Fact]
    public async Task RequestsWithoutRemoteIpAreNotLimited()
    {
        await using var factory = new RateLimitFactory(remoteIp: null);
        using var client = factory.CreateClient();

        for (var i = 0; i < 4; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/forgot-password", Request)).StatusCode);
    }

    [Fact]
    public async Task EmailBudgetIsIndependentOfIpAndRejectsUniformly()
    {
        await using var factory = new RateLimitFactory(IPAddress.Parse("203.0.113.20"), o =>
        {
            o.PermitLimit = 100;
            o.EmailPermitLimit = 2;
            o.EmailWindowSeconds = 3600;
        });
        using var client = factory.CreateClient();

        var first = new ForgotPasswordRequest("budget-a@test.invalid");
        await client.PostAsJsonAsync("/api/auth/forgot-password", first);
        await client.PostAsJsonAsync("/api/auth/forgot-password", first);
        var blocked = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(" Budget-A@Test.invalid "));
        var other = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("budget-b@test.invalid"));

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.True(blocked.Headers.Contains("Retry-After"));
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    [Fact]
    public async Task RefreshHasItsOwnBudgetAndDoesNotConsumeTheIpBudget()
    {
        await using var factory = new RateLimitFactory(IPAddress.Parse("203.0.113.30"), o =>
        {
            o.PermitLimit = 100;
            o.RefreshPermitLimit = 1;
            o.WindowSeconds = 3600;
        });
        using var client = factory.CreateClient();

        var refresh = new RefreshAuthRequest("x");
        var first = await client.PostAsJsonAsync("/api/auth/refresh", refresh);
        var second = await client.PostAsJsonAsync("/api/auth/refresh", refresh);
        var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", Request);

        Assert.NotEqual(HttpStatusCode.TooManyRequests, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.True(second.Headers.Contains("Retry-After"));
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
    }

[Fact]
    public async Task FullEmailCapacityReturnsTheSame429AndRetryAfterForOldAndNewEmails()
    {
        await using var factory = new RateLimitFactory(null, o => o.EmailPermitLimit = 1);
        using var client = factory.CreateClient();
        var limiter = factory.Services.GetRequiredService<AuthEmailRateLimiter>();
        for (var i = 0; i < AuthEmailRateLimiter.MaxPartitions; i++)
            Assert.True(limiter.TryAcquire(AuthEmailOperation.ForgotPassword, $"full-{i}@test.invalid", out _));

        using var oldEmail = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("full-0@test.invalid"));
        using var newEmail = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("unknown@test.invalid"));
        foreach (var response in new[] { oldEmail, newEmail })
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            Assert.True(response.Headers.RetryAfter?.Delta > TimeSpan.Zero);
            Assert.Contains(AuthRateLimiting.RejectionMessage, await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(await oldEmail.Content.ReadAsStringAsync(), await newEmail.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnlyTrustedProxyCanChangeTheIpBudgetWithForwardedHeaders(bool trusted)
    {
        var peer = IPAddress.Parse("203.0.113.40");
        await using var factory = new RateLimitFactory(peer, o =>
        {
            o.PermitLimit = 1;
            o.EmailPermitLimit = 100;
        }, services => services.Configure<ForwardedHeadersOptions>(o =>
        {
            if (trusted) o.KnownProxies.Add(peer);
        }));
        using var client = factory.CreateClient();
        async Task<HttpResponseMessage> SendAsync(string forwarded)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/forgot-password");
            request.Headers.Add("X-Forwarded-For", forwarded);
            request.Content = JsonContent.Create(Request);
            return await client.SendAsync(request);
        }
        using var first = await SendAsync("198.51.100.1");
        using var second = await SendAsync("198.51.100.2");
        using var repeated = await SendAsync("198.51.100.2");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(trusted ? HttpStatusCode.OK : HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, repeated.StatusCode);
        Assert.True(repeated.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Contains(AuthRateLimiting.RejectionMessage, await repeated.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OversizedEmailReturns429WithRetryAfter()
    {
        await using var factory = new RateLimitFactory(null);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(new string('a', 321)));
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Contains(AuthRateLimiting.RejectionMessage, await response.Content.ReadAsStringAsync());
    }

    private sealed class RateLimitFactory(IPAddress? remoteIp, Action<AuthRateLimitOptions>? tune = null, Action<IServiceCollection>? configure = null) : WebApplicationFactory<ServiceDesk.Api.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                foreach (var service in services.Where(d => d.ServiceType == typeof(IHostedService) &&
                             d.ImplementationType?.Name == "IdentityDataSeeder").ToArray())
                    services.Remove(service);
                services.RemoveAll<IForgotPassword>();
                services.AddScoped<IForgotPassword, AlwaysSucceedsForgotPassword>();
                services.RemoveAll<IRefreshAuth>();
                services.AddScoped<IRefreshAuth, AlwaysFailsRefresh>();
                services.Configure<AuthRateLimitOptions>(options =>
                {
                    options.PermitLimit = 2;
                    options.WindowSeconds = 3600;
                    tune?.Invoke(options);
                });
                services.AddSingleton<IStartupFilter>(new RemoteIpStartupFilter(remoteIp));
                configure?.Invoke(services);
            });
        }
    }

    /// <summary>Gán địa chỉ IP giả cho mọi request trước các middleware của ứng dụng.</summary>
    private sealed class RemoteIpStartupFilter(IPAddress? remoteIp) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = remoteIp;
                await nextMiddleware(context);
            });
            next(app);
        };
    }

    private sealed class AlwaysSucceedsForgotPassword : IForgotPassword
    {
        public Task<IdentityOperationResult> ExecuteAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(IdentityOperationResult.Success());
    }

    private sealed class AlwaysFailsRefresh : IRefreshAuth
    {
        public Task<AuthResult> ExecuteAsync(RefreshAuthRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(AuthResult.Failed("invalid"));
    }
}
