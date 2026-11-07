using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ServiceDesk.Api.Endpoints;
using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Identity.ForgotPassword;
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

    private sealed class RateLimitFactory(IPAddress? remoteIp) : WebApplicationFactory<ServiceDesk.Api.Program>
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
                services.Configure<AuthRateLimitOptions>(options =>
                {
                    options.PermitLimit = 2;
                    options.WindowSeconds = 3600;
                });
                services.AddSingleton<IStartupFilter>(new RemoteIpStartupFilter(remoteIp));
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
}
