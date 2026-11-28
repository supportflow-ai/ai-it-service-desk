using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using ServiceDesk.Modules.Identity.Infrastructure.Options;
using ServiceDesk.Platform.Persistence;
using Xunit;
namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Unit")]
public sealed class StartupBoundaryTests
{
    [Theory]
    [InlineData("")] [InlineData("short")] [InlineData("CHANGE_ME_development_secret_key_at_least_32_chars")]
    public void ProductionRejectsMissingShortOrSampleSigningKey(string secret)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns("Production");
        Assert.True(new JwtOptionsValidator(environment.Object).Validate(null, new JwtOptions { SecretKey = secret }).Failed);
    }

    [Fact]
    public async Task ApiStartsWithoutDatabase_DoesNotMigrateOrSeed_AndLivenessIsHealthy()
    {
        await using var factory = new Factory("valid_production_signing_key_0123456789abcdef");
        using var client = factory.CreateClient();
        Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.DoesNotContain(factory.Services.GetServices<IHostedService>(), service => service.GetType().Name.Contains("Seeder"));
        using var scope = factory.Services.CreateScope();
        Assert.Empty(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ChangeTracker.Entries());
    }

    [Fact]
    public void ApiStartupFailsForProductionSampleKey()
    {
        using var factory = new Factory("CHANGE_ME_development_secret_key_at_least_32_chars");
        Assert.ThrowsAny<OptionsValidationException>(() => factory.CreateClient());
    }

[Theory]
    [InlineData("")]
    [InlineData("invalid")]
    public void ApiStartupFailsWhenOtpKeyIsMissingOrInvalid(string key)
    {
        using var factory = new Factory("valid_production_signing_key_0123456789abcdef", key);
        Assert.ThrowsAny<OptionsValidationException>(() => factory.CreateClient());
    }

[Fact]
    public async Task ForgotPasswordCannotAcknowledgeARequestThatWasNotPersisted()
    {
        await using var factory = new Factory("valid_production_signing_key_0123456789abcdef");
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "durable@test.invalid" });
        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("Npgsql", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ForgotPasswordCannotAcknowledgeAnEmptyEmail()
    {
        await using var factory = new Factory("valid_production_signing_key_0123456789abcdef");
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = " " });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class Factory(string secret, string? otpKey = null) : WebApplicationFactory<ServiceDesk.Api.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = secret,
                ["Otp:Key"] = otpKey ?? OtpTestSettings.Key,
                ["Database:ConnectionString"] = "Host=127.0.0.1;Port=1;Database=unavailable;Username=test;Password=test;Timeout=1"
            }));
        }
    }
}
