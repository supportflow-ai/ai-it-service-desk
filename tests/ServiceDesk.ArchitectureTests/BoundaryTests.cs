using System.Reflection;
using NetArchTest.Rules;
using ServiceDesk.Application.Identity.RegisterUser;
using ServiceDesk.Application.Ticketing.CreateTicket;
using ServiceDesk.Domain.AIAssistance;
using ServiceDesk.Infrastructure.Persistence;
using Xunit;
namespace ServiceDesk.ArchitectureTests;

public sealed class BoundaryTests
{
    [Fact]
    public void ApplicationUseCasesBelongToApplicationAssembly()
    {
        var application = typeof(ServiceDesk.Application.DependencyInjection).Assembly;
        Assert.Same(application, typeof(ICreateTicket).Assembly);
        Assert.Same(application, typeof(IRegisterUser).Assembly);
        Assert.DoesNotContain(typeof(ApplicationDbContext).Assembly.GetTypes(),
            type => type.Namespace?.StartsWith("ServiceDesk.Application", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")] [InlineData("Npgsql")]
    [InlineData("Microsoft.AspNetCore")] [InlineData("ServiceDesk.Infrastructure")]
    public void ApplicationCannotDependOnFrameworkAdapters(string forbidden)
    {
        Assert.True(Types.InAssembly(typeof(ICreateTicket).Assembly).ShouldNot().HaveDependencyOn(forbidden).GetResult().IsSuccessful);
    }

    [Fact]
    public void EndpointsCannotAccessInfrastructureOrEf()
    {
        foreach (var forbidden in new[] { "ServiceDesk.Infrastructure", "Microsoft.EntityFrameworkCore", "Npgsql" })
            Assert.True(Types.InAssembly(typeof(ServiceDesk.Api.Program).Assembly).That()
                .ResideInNamespace("ServiceDesk.Api.Endpoints").ShouldNot().HaveDependencyOn(forbidden).GetResult().IsSuccessful);
    }

    [Fact]
    public void AiDomainCannotAccessTicketEntitiesOrIdentity()
    {
        foreach (var forbidden in new[] { "ServiceDesk.Domain.Ticketing.Ticket", "ServiceDesk.Domain.Identity" })
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
