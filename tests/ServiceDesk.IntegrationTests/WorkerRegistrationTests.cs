using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using ServiceDesk.Modules.AIAssistance.Application.Ports;
using ServiceDesk.Modules.AIAssistance.Infrastructure;
using ServiceDesk.Infrastructure;
using Xunit;

namespace ServiceDesk.IntegrationTests;

public sealed class WorkerRegistrationTests
{
    [Fact]
    public void WorkerResolvesConfiguredHttpAdapterWithoutCallingAiOrImpersonatingUser()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ai:Enabled"] = "true", ["Ai:Endpoint"] = "http://ai-service:8000",
            ["Ai:BearerToken"] = "synthetic-service-token-at-least-32-characters"
        });
        ServiceDesk.Worker.WorkerServices.AddWorkerServices(builder.Services, builder.Configuration);
        using var host = builder.Build();
        using var scope = host.Services.CreateScope();
        Assert.IsType<HttpTicketClassificationService>(scope.ServiceProvider.GetRequiredService<ITicketClassificationService>());
        Assert.False(scope.ServiceProvider.GetRequiredService<ServiceDesk.SharedKernel.Application.Abstractions.ICurrentUser>().IsAuthenticated);
    }

    [Fact]
    public async Task ActualWorkerStartsInDevelopmentWithoutDatabaseOrHttpContext()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Development" });
        ServiceDesk.Worker.WorkerServices.AddWorkerServices(builder.Services, builder.Configuration);
        using var host = builder.Build();
        await host.StartAsync();
        using var scope = host.Services.CreateScope();
        Assert.False(scope.ServiceProvider.GetRequiredService<ServiceDesk.SharedKernel.Application.Abstractions.ICurrentUser>().IsAuthenticated);
        Assert.Null(scope.ServiceProvider.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ServiceDesk.Modules.Ticketing.Application.CreateTicket.ICreateTicket>());
        await host.StopAsync();
    }

    [Fact]
    public void GenericHost_CanResolveIdentityWithoutWebHostDefaults()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddInfrastructure(builder.Configuration).AddIdentityStores();
        using var host = builder.Build();
        using var scope = host.Services.CreateScope();
        // Generic Worker hosts do not implicitly register the Web host's services.
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
    }
}
