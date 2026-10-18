using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceDesk.Infrastructure;
using Xunit;

namespace ServiceDesk.IntegrationTests;

public sealed class WorkerRegistrationTests
{
    [Fact]
    public void GenericHost_CanResolveIdentityWithoutWebHostDefaults()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddInfrastructure(builder.Configuration);
        using var host = builder.Build();
        using var scope = host.Services.CreateScope();
        // Generic Worker hosts do not implicitly register the Web host's services.
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
    }
}
