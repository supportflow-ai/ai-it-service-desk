using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceDesk.Infrastructure;
using ServiceDesk.Infrastructure.Persistence;

if (args.Length == 0 || args[0] is not ("migrate" or "seed-roles" or "seed-demo" or "bootstrap-admin" or "check-production"))
{
    Console.Error.WriteLine("Usage: ServiceDesk.Database migrate|seed-roles|seed-demo|bootstrap-admin|check-production");
    return 2;
}
var builder = Host.CreateApplicationBuilder(args.Skip(1).ToArray());
// Provisioning logs must never include connection strings, bootstrap secrets or SQL parameters.
builder.Logging.ClearProviders();
builder.Services.AddPersistence(builder.Configuration).AddIdentityStores();
builder.Services.AddScoped<DatabaseMaintenance>();
using var host = builder.Build();
await using var scope = host.Services.CreateAsyncScope();
var maintenance = scope.ServiceProvider.GetRequiredService<DatabaseMaintenance>();
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
try
{
    var ct = cancellation.Token;
    switch (args[0])
    {
        case "migrate": await maintenance.MigrateAsync(ct); break;
        case "seed-roles": await maintenance.SeedRolesAsync(ct); break;
        case "seed-demo":
            await maintenance.SeedDemoAsync(builder.Environment.EnvironmentName,
                builder.Configuration.GetValue<bool>("SeedDemo:Enabled"), builder.Configuration["SeedDemo:Password"] ?? "", ct); break;
        case "bootstrap-admin":
            await maintenance.BootstrapAdminAsync(Environment.GetEnvironmentVariable("BootstrapAdmin__Email") ?? "",
                Environment.GetEnvironmentVariable("BootstrapAdmin__Password") ?? "", ct); break;
        case "check-production": await maintenance.CheckProductionAsync(ct); break;
    }
    Console.WriteLine($"{args[0]} completed.");
    return 0;
}
catch (DatabaseMaintenanceException exception)
{
    Console.Error.WriteLine($"{args[0]} failed [{exception.Message}]. No credentials were logged.");
    return 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"{args[0]} failed ({exception.GetType().Name}). Check database availability, role setup and securely supplied configuration; no credentials were logged.");
    return 1;
}
