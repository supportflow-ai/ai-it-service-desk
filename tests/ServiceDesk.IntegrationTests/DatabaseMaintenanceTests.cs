using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ServiceDesk.Domain.Ticketing;
using ServiceDesk.Infrastructure.Persistence;
using Xunit;
namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class DatabaseMaintenanceTests(ServiceDeskWebApplicationFactory factory) : IClassFixture<ServiceDeskWebApplicationFactory>
{
    [Fact]
    public async Task ProvisioningIsExplicitIdempotentAndNeverElevatesExistingUsers()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var maintenance = ActivatorUtilities.CreateInstance<DatabaseMaintenance>(scope.ServiceProvider);
        await maintenance.MigrateAsync(); await maintenance.MigrateAsync();
        await maintenance.SeedRolesAsync(); await maintenance.SeedRolesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => maintenance.SeedDemoAsync("Production", true, "Test@1234"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => maintenance.SeedDemoAsync("Development", false, "Test@1234"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => maintenance.BootstrapAdminAsync("requester@test.com", "Test@1234"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => maintenance.CheckProductionAsync());
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var requester = (await users.FindByEmailAsync("requester@test.com"))!;
        Assert.Equal(new[] { "Requester" }, await users.GetRolesAsync(requester));
    }

    [Fact]
    public async Task TicketNumberWidthDoesNotTruncateOrResetSequence()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.ExecuteSqlRawAsync("SELECT setval('public.ticket_number_seq', 9998, true)");
        var numbers = new List<string>();
        foreach (var expected in new[] { 9999, 10000, 10001 })
        {
            var ticket = Ticket.Create(Guid.NewGuid(), "Number width", "A sufficiently long description", "NET", DateTime.UtcNow);
            context.Tickets.Add(ticket); await context.SaveChangesAsync();
            Assert.EndsWith($"-{expected}", ticket.TicketNumber);
            numbers.Add(ticket.TicketNumber);
        }
        Assert.Equal(3, numbers.Distinct().Count());
        var before = await context.Database.SqlQuery<long>($"SELECT last_value AS \"Value\" FROM public.ticket_number_seq").SingleAsync();
        await ActivatorUtilities.CreateInstance<DatabaseMaintenance>(scope.ServiceProvider).MigrateAsync();
        var after = await context.Database.SqlQuery<long>($"SELECT last_value AS \"Value\" FROM public.ticket_number_seq").SingleAsync();
        Assert.Equal(before, after);
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
