using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Infrastructure.Persistence;
using ServiceDesk.Infrastructure.Ticketing;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Unit")]
public sealed class PersistenceContractTests
{
    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused").Options);

    [Fact]
    public void CurrentModelMatchesReleasedMigrationSnapshot()
    {
        using var context = Context();
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task NullScopeNeverBecomesUnrestrictedQuery()
    {
        await using var context = Context();
        await Assert.ThrowsAsync<ArgumentException>(() => new PostgresTicketQueries(context)
            .GetByIdAsync(Guid.NewGuid(), null!, default));
        Assert.Throws<ArgumentException>(() => new TicketReadScope.OwnTickets(Guid.Empty));
    }
}
