using System.Reflection;
using Mono.Cecil;
using NetArchTest.Rules;
using Xunit;

namespace ServiceDesk.ArchitectureTests;

public sealed class ModuleEncapsulationTests
{
    /// <summary>Mọi assembly production của backend sau khi tổ chức lại theo module.</summary>
    internal static readonly string[] ProductionAssemblies =
    [
        "ServiceDesk.SharedKernel", "ServiceDesk.SharedKernel.Web",
        "ServiceDesk.Modules.Identity", "ServiceDesk.Modules.Ticketing", "ServiceDesk.Modules.Knowledge", "ServiceDesk.Modules.AIAssistance",
        "ServiceDesk.Infrastructure", "ServiceDesk.Api", "ServiceDesk.Worker", "ServiceDesk.Database"
    ];

    internal static IEnumerable<Assembly> ModuleAssemblies() =>
        ModuleRules.Modules.Select(module => Assembly.Load($"ServiceDesk.Modules.{module}"));

    [Fact]
    public void AllProductionTypesRespectOwnershipAndPublishedContracts()
    {
        var errors = new List<string>();
        foreach (var name in ProductionAssemblies)
        {
            using var assembly = AssemblyDefinition.ReadAssembly(Assembly.Load(name).Location);
            errors.AddRange(ModuleRules.Check(assembly.MainModule.Types.SelectMany(ModuleRules.Flatten)));
        }
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public void PublishedContractsExistAndArePublic()
    {
        var types = ModuleAssemblies().SelectMany(assembly => assembly.GetTypes())
            .ToDictionary(t => t.FullName!, StringComparer.Ordinal);
        foreach (var name in ModuleRules.Published)
            Assert.True(types.TryGetValue(name, out var type) && type.IsPublic, name);
    }

    [Fact]
    public void EachModuleTypeLivesInItsOwnAssembly()
    {
        foreach (var module in ModuleRules.Modules)
        {
            var assembly = Assembly.Load($"ServiceDesk.Modules.{module}");
            Assert.All(assembly.GetTypes().Where(t => t.Namespace is not null && t.Namespace.StartsWith("ServiceDesk.", StringComparison.Ordinal)),
                type => Assert.StartsWith($"ServiceDesk.Modules.{module}", type.Namespace!, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void UseCasesAreInternalAndWireTypesBelongOnlyToInfrastructure()
    {
        var application = ModuleAssemblies().SelectMany(assembly => assembly.GetTypes())
            .Where(t => t.Namespace?.Contains(".Application", StringComparison.Ordinal) == true).ToArray();
        var cases = application.Where(t => t.Name.EndsWith("UseCase", StringComparison.Ordinal)).ToArray();
        Assert.Equal(36, cases.Length);
        Assert.All(cases, type => Assert.True(type.IsNotPublic && type.IsSealed, type.FullName));
        Assert.DoesNotContain(application, type => type.Name.StartsWith("ClassificationContract", StringComparison.Ordinal));
        // Ticket idempotency deliberately retains its versioned JSON fingerprint encoding.
        Assert.True(Types.InAssembly(Assembly.Load("ServiceDesk.Modules.AIAssistance")).That()
            .ResideInNamespace("ServiceDesk.Modules.AIAssistance.Application")
            .ShouldNot().HaveDependencyOn("System.Text.Json").GetResult().IsSuccessful);
    }
}
