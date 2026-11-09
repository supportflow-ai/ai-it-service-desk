using System.Reflection;
using NetArchTest.Rules;
using ServiceDesk.Platform.Persistence;
using ServiceDesk.Modules.AIAssistance.Domain;
using ServiceDesk.Modules.Identity;
using ServiceDesk.Modules.Identity.Application.RegisterUser;
using ServiceDesk.Modules.Ticketing;
using ServiceDesk.Modules.Ticketing.Application.CreateTicket;
using Xunit;
namespace ServiceDesk.ArchitectureTests;

public sealed class BoundaryTests
{
    [Fact]
    public void UseCasesBelongToTheirModuleAndNotToSharedInfrastructure()
    {
        Assert.Same(typeof(TicketingModule).Assembly, typeof(ICreateTicket).Assembly);
        Assert.Same(typeof(IdentityModule).Assembly, typeof(IRegisterUser).Assembly);
        Assert.DoesNotContain(typeof(ApplicationDbContext).Assembly.GetTypes(),
            type => type.Namespace?.StartsWith("ServiceDesk.Modules", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")] [InlineData("Npgsql")]
    [InlineData("Microsoft.AspNetCore")] [InlineData("ServiceDesk.Platform")]
    public void ApplicationCannotDependOnFrameworkAdapters(string forbidden)
    {
        foreach (var module in ModuleRules.Modules)
            Assert.True(Types.InAssembly(Assembly.Load($"ServiceDesk.Modules.{module}")).That()
                .ResideInNamespace($"ServiceDesk.Modules.{module}.Application")
                .ShouldNot().HaveDependencyOn(forbidden).GetResult().IsSuccessful, $"{module}: {forbidden}");
    }

    [Fact]
    public void EndpointsCannotAccessInfrastructureOrEf()
    {
        foreach (var forbidden in new[] { "ServiceDesk.Platform", "Microsoft.EntityFrameworkCore", "Npgsql" })
        {
            Assert.True(Types.InAssembly(typeof(ServiceDesk.Api.Program).Assembly).That()
                .ResideInNamespace("ServiceDesk.Api.Endpoints").ShouldNot().HaveDependencyOn(forbidden).GetResult().IsSuccessful);
            foreach (var module in ModuleRules.Modules)
                Assert.True(Types.InAssembly(Assembly.Load($"ServiceDesk.Modules.{module}")).That()
                    .ResideInNamespace($"ServiceDesk.Modules.{module}.Presentation")
                    .ShouldNot().HaveDependencyOn(forbidden).GetResult().IsSuccessful, $"{module}: {forbidden}");
        }
    }

    [Theory]
    [InlineData("Ticketing")] [InlineData("Knowledge")] [InlineData("AIAssistance")]
    public void OnlyIdentityReadsAspNetCoreIdentityTables(string module)
    {
        // Module khác tra người dùng qua IUserDirectory do Identity công bố.
        Assert.True(Types.InAssembly(Assembly.Load($"ServiceDesk.Modules.{module}"))
            .ShouldNot().HaveDependencyOn("Microsoft.AspNetCore.Identity").GetResult().IsSuccessful, module);
    }

    [Fact]
    public void AiDomainCannotAccessTicketEntitiesOrIdentity()
    {
        foreach (var forbidden in new[] { "ServiceDesk.Modules.Ticketing.Domain.Ticket", "ServiceDesk.SharedKernel.Authorization" })
        {
            // AI policies may consume only the published Ticketing value types, never the Ticket aggregate.
            var types = new[] { typeof(AIAnalysis), typeof(AISuggestion), typeof(TriagePolicy) };
            foreach (var type in types)
                Assert.True(Types.InAssembly(type.Assembly).That().HaveName(type.Name).ShouldNot()
                    .HaveDependencyOn(forbidden).GetResult().IsSuccessful);
        }
    }

    [Fact]
    public void WorkerDoesNotReferenceHttpOrApi()
    {
        var worker = Assembly.Load("ServiceDesk.Worker");
        foreach (var forbidden in new[] { "Microsoft.AspNetCore.Http", "ServiceDesk.Api" })
            Assert.True(Types.InAssembly(worker).ShouldNot().HaveDependencyOn(forbidden).GetResult().IsSuccessful);
    }
}
