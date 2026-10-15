using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Infrastructure.Persistence;
using Xunit;

namespace ServiceDesk.IntegrationTests;

/// <summary>
/// Tests that the database context can connect and run migrations
/// against a real PostgreSQL instance via Testcontainers.
/// </summary>
[Trait("Category", "Integration")]
public class DatabaseTests : IClassFixture<ServiceDeskWebApplicationFactory>
{
    private readonly ServiceDeskWebApplicationFactory _factory;

    public DatabaseTests(ServiceDeskWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DbContext_CanConnect_AndMigrateIdentitySchema()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var canConnect = await dbContext.Database.CanConnectAsync();
        canConnect.Should().BeTrue("Should be able to connect to PostgreSQL Testcontainer");
        (await dbContext.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await dbContext.Roles.CountAsync()).Should().Be(3);
        (await dbContext.Users.CountAsync()).Should().Be(3);
    }
}
