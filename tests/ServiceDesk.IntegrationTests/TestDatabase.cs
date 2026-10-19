using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Infrastructure;
using ServiceDesk.Infrastructure.Persistence;
namespace ServiceDesk.IntegrationTests;
internal static class TestDatabase
{
    internal static async Task PrepareAsync(string connectionString, bool demo)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Database:ConnectionString"] = connectionString }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(configuration).AddIdentityStores();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var maintenance = ActivatorUtilities.CreateInstance<DatabaseMaintenance>(scope.ServiceProvider);
        await maintenance.MigrateAsync();
        await maintenance.SeedRolesAsync();
        if (demo) await maintenance.SeedDemoAsync("Test", true, "Test@1234");
    }
}
