using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ServiceDesk.Infrastructure.Identity;
using ServiceDesk.Infrastructure.Clock;
using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Api.Identity;
using ServiceDesk.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace ServiceDesk.IntegrationTests;

/// <summary>
/// Two independent real API hosts, one PostgreSQL database. No fake ticket/idempotency service.
/// Database is provisioned explicitly before starting either host.
/// JWT is validated by the production API middleware with isolated fixture keys.
/// </summary>
public sealed class TicketIdempotencyPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ticket_idempotency_tests").WithUsername("test").WithPassword("test").Build();

    public string ConnectionString => _postgres.GetConnectionString();
    public const string Secret = "postgres_idempotency_test_secret_01234567890123456789";
    public Factory? First { get; private set; }
    public Factory? Second { get; private set; }

    public ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var context = CreateContext();
        await TestDatabase.PrepareAsync(ConnectionString, demo: false);
        First = new Factory(ConnectionString);
        Second = new Factory(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        if (First is not null) await First.DisposeAsync();
        if (Second is not null) await Second.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public sealed class Factory(string connectionString, Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor? interceptor = null) : WebApplicationFactory<ServiceDesk.Api.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = connectionString,
                    ["Jwt:SecretKey"] = Secret,
                    ["Jwt:Issuer"] = "ServiceDesk",
                    ["Jwt:Audience"] = "ServiceDeskClient"
                }));
            builder.ConfigureServices(services =>
            {

                // Program captures JwtOptions before WebApplicationFactory's configuration callback.
                // Override only fixture issuer/audience/key, retaining the real JWT handler and all validation flags.
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters.ValidIssuer = "ServiceDesk";
                    options.TokenValidationParameters.ValidAudience = "ServiceDeskClient";
                    options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
                });
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseNpgsql(connectionString);
                    if (interceptor is not null) options.AddInterceptors(interceptor);
                });
            });
        }
    }
}
