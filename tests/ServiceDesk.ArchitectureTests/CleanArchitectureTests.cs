using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace ServiceDesk.ArchitectureTests;

/// <summary>
/// Luật Clean Architecture áp dụng bên trong từng module:
/// Domain không phụ thuộc tầng nào khác hay framework; Application không phụ thuộc Infrastructure/Presentation;
/// Presentation không chạm Infrastructure; SharedKernel không phụ thuộc framework web hay persistence.
/// </summary>
public class CleanArchitectureTests
{
    public static TheoryData<string> Modules()
    {
        var data = new TheoryData<string>();
        foreach (var module in ModuleRules.Modules) data.Add(module);
        return data;
    }

    private static Types Module(string module) => Types.InAssembly(Assembly.Load($"ServiceDesk.Modules.{module}"));

    [Theory]
    [MemberData(nameof(Modules))]
    public void Domain_ShouldNotReference_OtherLayersOrFrameworks(string module)
    {
        foreach (var forbidden in new[]
                 {
                     $"ServiceDesk.Modules.{module}.Application", $"ServiceDesk.Modules.{module}.Infrastructure",
                     $"ServiceDesk.Modules.{module}.Presentation", "ServiceDesk.Infrastructure", "ServiceDesk.Api",
                     "Microsoft.EntityFrameworkCore", "Npgsql", "Microsoft.AspNetCore"
                 })
        {
            Module(module).That().ResideInNamespace($"ServiceDesk.Modules.{module}.Domain")
                .ShouldNot().HaveDependencyOn(forbidden).GetResult().IsSuccessful
                .Should().BeTrue($"Domain của {module} không được phụ thuộc {forbidden}");
        }
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Application_ShouldNotReference_InfrastructureOrPresentation(string module)
    {
        foreach (var forbidden in new[]
                 {
                     $"ServiceDesk.Modules.{module}.Infrastructure", $"ServiceDesk.Modules.{module}.Presentation",
                     "ServiceDesk.Infrastructure", "ServiceDesk.Api", "ServiceDesk.SharedKernel.Web",
                     "Microsoft.EntityFrameworkCore", "Npgsql", "Microsoft.AspNetCore"
                 })
        {
            Module(module).That().ResideInNamespace($"ServiceDesk.Modules.{module}.Application")
                .ShouldNot().HaveDependencyOn(forbidden).GetResult().IsSuccessful
                .Should().BeTrue($"Application của {module} không được phụ thuộc {forbidden}");
        }
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Presentation_ShouldNotReference_Persistence(string module)
    {
        foreach (var forbidden in new[]
                 {
                     $"ServiceDesk.Modules.{module}.Infrastructure", "ServiceDesk.Infrastructure",
                     "Microsoft.EntityFrameworkCore", "Npgsql"
                 })
        {
            Module(module).That().ResideInNamespace($"ServiceDesk.Modules.{module}.Presentation")
                .ShouldNot().HaveDependencyOn(forbidden).GetResult().IsSuccessful
                .Should().BeTrue($"Presentation của {module} không được phụ thuộc {forbidden}");
        }
    }

    [Fact]
    public void SharedKernel_ShouldNotReference_ModulesOrFrameworks()
    {
        foreach (var forbidden in new[]
                 {
                     "ServiceDesk.Modules", "ServiceDesk.Infrastructure", "ServiceDesk.Api",
                     "Microsoft.EntityFrameworkCore", "Npgsql", "Microsoft.AspNetCore"
                 })
        {
            Types.InAssembly(Assembly.Load("ServiceDesk.SharedKernel")).ShouldNot().HaveDependencyOn(forbidden)
                .GetResult().IsSuccessful.Should().BeTrue($"SharedKernel không được phụ thuộc {forbidden}");
        }
    }
}
