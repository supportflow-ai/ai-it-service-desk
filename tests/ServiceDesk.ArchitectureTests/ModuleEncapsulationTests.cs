using System.Reflection;
using Mono.Cecil;
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
}
