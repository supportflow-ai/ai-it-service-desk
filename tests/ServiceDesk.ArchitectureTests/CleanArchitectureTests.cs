using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace ServiceDesk.ArchitectureTests;

/// <summary>
/// Enforces Clean Architecture dependency rules.
/// Domain must not reference any framework or infrastructure.
/// Application must not reference Infrastructure or Presentation.
/// </summary>
public class CleanArchitectureTests
{
    private const string DomainNamespace = "ServiceDesk.Domain";
    private const string ApplicationNamespace = "ServiceDesk.Application";
    private const string InfrastructureNamespace = "ServiceDesk.Infrastructure";
    private const string ApiNamespace = "ServiceDesk.Api";

    [Fact]
    public void Domain_ShouldNotReference_Infrastructure()
    {
        var result = Types.InAssembly(typeof(ServiceDesk.Domain.Common.DomainDefaults).Assembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Domain must not depend on Infrastructure");
    }

    [Fact]
    public void Domain_ShouldNotReference_Application()
    {
        var result = Types.InAssembly(typeof(ServiceDesk.Domain.Common.DomainDefaults).Assembly)
            .ShouldNot()
            .HaveDependencyOn(ApplicationNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Domain must not depend on Application");
    }

    [Fact]
    public void Domain_ShouldNotReference_EFCore()
    {
        var result = Types.InAssembly(typeof(ServiceDesk.Domain.Common.DomainDefaults).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Domain must not depend on EF Core");
    }

    [Fact]
    public void Domain_ShouldNotReference_AspNetCore()
    {
        var result = Types.InAssembly(typeof(ServiceDesk.Domain.Common.DomainDefaults).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Domain must not depend on ASP.NET Core");
    }

    [Fact]
    public void Application_ShouldNotReference_Infrastructure()
    {
        var result = Types.InAssembly(typeof(ServiceDesk.Application.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Application must not depend on Infrastructure");
    }

    [Fact]
    public void Application_ShouldNotReference_Api()
    {
        var result = Types.InAssembly(typeof(ServiceDesk.Application.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Application must not depend on API/Presentation");
    }
}
