using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ServiceDesk.Domain.Ticketing;
using ServiceDesk.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class IdentityMigrationTests
{
    [Fact]
    public async Task TicketOnlyDatabase_UpgradesIdentity_AndRollbackReapplyPreservesData()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
        var migrator = context.GetService<IMigrator>();
        const string ticketBaseline = "20261014230000_AddRequesterCreatedAtIdIndex";
        await migrator.MigrateAsync(ticketBaseline);

        var ticketId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO public.tickets (id, requester_id, title, description, category_id, status, created_at, updated_at) VALUES ({ticketId}, {requesterId}, 'Existing ticket', 'Existing persisted ticket description', 'NET', 4, {now}, {now})");
        var ticketNumber = await context.Database.SqlQuery<string>(
            $"SELECT ticket_number AS \"Value\" FROM public.tickets WHERE id = {ticketId}").SingleAsync();

        await context.Database.MigrateAsync();
        var account = new IdentityUser { Id = Guid.NewGuid().ToString(), UserName = "existing", NormalizedUserName = "EXISTING" };
        context.Users.Add(account);
        context.Roles.Add(new IdentityRole("ExistingRole"));
        await context.SaveChangesAsync();

        // Down retains Identity tables so a rollback cannot erase accounts, including
        // tables provisioned before this repair. Up must tolerate those existing tables.
        await migrator.MigrateAsync(ticketBaseline);
        await context.Database.MigrateAsync();
        context.ChangeTracker.Clear();
        (await context.Users.SingleAsync()).Id.Should().Be(account.Id);
        (await context.Roles.SingleAsync()).Name.Should().Be("ExistingRole");
        var persisted = await context.Tickets.SingleAsync();
        persisted.TicketNumber.Should().Be(ticketNumber);
        ((int)persisted.Status).Should().Be(4);
        (await context.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
    }
}
