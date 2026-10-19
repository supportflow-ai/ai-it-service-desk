using System.Reflection;
using Mono.Cecil;
using NetArchTest.Rules;
using Xunit;

namespace ServiceDesk.ArchitectureTests;

public sealed class ModuleEncapsulationTests
{
    [Fact]
    public void AllProductionTypesRespectOwnershipAndPublishedContracts()
    {
        var errors = new List<string>();
        foreach (var name in new[] { "Domain", "Application", "Infrastructure", "Api", "Worker", "Database" })
        {
            using var assembly = AssemblyDefinition.ReadAssembly(Assembly.Load($"ServiceDesk.{name}").Location);
            errors.AddRange(ModuleRules.Check(assembly.MainModule.Types.SelectMany(ModuleRules.Flatten)));
        }
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public void PublishedContractsExistAndArePublic()
    {
        var types = new[] { "Domain", "Application" }.SelectMany(name => Assembly.Load($"ServiceDesk.{name}").GetTypes())
            .ToDictionary(t => t.FullName!, StringComparer.Ordinal);
        foreach (var name in ModuleRules.Published)
            Assert.True(types.TryGetValue(name, out var type) && type.IsPublic, name);
    }

    [Fact]
    public void SixUseCasesAreInternalAndWireTypesBelongOnlyToInfrastructure()
    {
        var application = Assembly.Load("ServiceDesk.Application");
        var cases = application.GetTypes().Where(t => t.Name.EndsWith("UseCase", StringComparison.Ordinal)).ToArray();
        Assert.Equal(6, cases.Length);
        Assert.All(cases, type => Assert.True(type.IsNotPublic && type.IsSealed, type.FullName));
        Assert.DoesNotContain(application.GetTypes(), type => type.Name.StartsWith("ClassificationContract", StringComparison.Ordinal));
        // Ticket idempotency deliberately retains its versioned JSON fingerprint encoding.
        Assert.True(Types.InAssembly(application).That().ResideInNamespace("ServiceDesk.Application.AIAssistance")
            .ShouldNot().HaveDependencyOn("System.Text.Json").GetResult().IsSuccessful);
    }
}
