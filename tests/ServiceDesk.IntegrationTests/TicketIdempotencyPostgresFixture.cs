using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ServiceDesk.Infrastructure.Identity;
using ServiceDesk.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace ServiceDesk.IntegrationTests;

/// <summary>
/// Two independent real API hosts, one PostgreSQL database. No fake ticket/idempotency service.
/// IdentityDataSeeder is excluded only here: the pre-existing migration has no Identity tables.
/// JWT is still validated by the production API middleware; baseline Auth tests remain unchanged.
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
        await context.Database.MigrateAsync();
        First = new Factory(ConnectionString);
        Second = new Factory(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        if (First is not null) await First.DisposeAsync();
        if (Second is not null) await Second.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public sealed class Factory(string connectionString) : WebApplicationFactory<ServiceDesk.Api.Program>
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
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
                foreach (var descriptor in services.Where(descriptor =>
                    descriptor.ServiceType == typeof(IHostedService) &&
                    descriptor.ImplementationType == typeof(IdentityDataSeeder)).ToArray())
                    services.Remove(descriptor);
            });
        }
    }
}
