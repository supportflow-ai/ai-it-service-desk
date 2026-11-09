using System.Reflection;
using System.Text.RegularExpressions;
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
        // Kiểu do trình biên dịch sinh (kiểu ẩn danh, closure) không có namespace và có thể trùng tên giữa các assembly.
        var types = ModuleAssemblies().SelectMany(assembly => assembly.GetTypes())
            .Where(t => t.Namespace?.StartsWith("ServiceDesk.", StringComparison.Ordinal) == true)
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

    /// <summary>Tên module sở hữu assembly chứa kiểu (ServiceDesk.Modules.{Module}).</summary>
    private static string ModuleOf(Type type) =>
        type.Assembly.GetName().Name!["ServiceDesk.Modules.".Length..];

    [Fact]
    public void UseCasesAreInternalAndWireTypesBelongOnlyToInfrastructure()
    {
        var application = ModuleAssemblies().SelectMany(assembly => assembly.GetTypes())
            .Where(t => t.Namespace?.Contains(".Application", StringComparison.Ordinal) == true).ToArray();
        var cases = application.Where(t => t.Name.EndsWith("UseCase", StringComparison.Ordinal)).ToArray();
        // Không cố định số lượng use case: thêm use case hợp lệ không được làm CI đỏ. Quy tắc cần giữ là mỗi lớp
        // UseCase phải internal sealed (chỉ lộ ra qua cổng đã công bố) và nằm trong tầng Application của đúng module
        // sở hữu assembly đó.
        Assert.NotEmpty(cases);
        Assert.All(cases, type => Assert.True(type.IsNotPublic && type.IsSealed, type.FullName));
        Assert.All(cases, type => Assert.Matches(
            $@"^ServiceDesk\.Modules\.{Regex.Escape(ModuleOf(type))}\.Application(\.|$)", type.Namespace!));
        Assert.DoesNotContain(application, type => type.Name.StartsWith("ClassificationContract", StringComparison.Ordinal));
        // Ticket idempotency deliberately retains its versioned JSON fingerprint encoding.
        Assert.True(Types.InAssembly(Assembly.Load("ServiceDesk.Modules.AIAssistance")).That()
            .ResideInNamespace("ServiceDesk.Modules.AIAssistance.Application")
            .ShouldNot().HaveDependencyOn("System.Text.Json").GetResult().IsSuccessful);
    }
}
