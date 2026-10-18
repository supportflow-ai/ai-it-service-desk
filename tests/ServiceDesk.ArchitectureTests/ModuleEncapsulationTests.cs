using Mono.Cecil;
using Mono.Cecil.Cil;
using ServiceDesk.Application.Ticketing;
using ServiceDesk.Domain.AIAssistance;
using ServiceDesk.Infrastructure.Persistence;
using Xunit;

namespace ServiceDesk.ArchitectureTests;

public sealed class ModuleEncapsulationTests
{
    [Fact]
    public void ModuleReferencesUseOnlyPublishedCrossModuleValueTypes()
    {
        var violations = new List<string>();
        foreach (var assembly in new[] { typeof(AIAnalysis).Assembly, typeof(TicketService).Assembly, typeof(ApplicationDbContext).Assembly })
        {
            using var definition = AssemblyDefinition.ReadAssembly(assembly.Location);
            foreach (var type in definition.MainModule.Types.SelectMany(Flatten))
            {
                var source = Module(type.FullName);
                if (source is null) continue;
                foreach (var target in References(type).SelectMany(Expand))
                {
                    var other = Module(target.FullName);
                    if (other is null || source == other || IsPublishedValue(target.FullName)) continue;
                    violations.Add($"{type.FullName} -> {target.FullName}");
                }
            }
        }
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations.Distinct()));
    }

    private static string? Module(string name)
    {
        foreach (var layer in new[] { "Domain", "Application", "Infrastructure" })
            foreach (var module in new[] { "Ticketing", "Identity", "AIAssistance", "AI" })
                if (name.StartsWith($"ServiceDesk.{layer}.{module}.", StringComparison.Ordinal))
                    return module == "AI" ? "AIAssistance" : module;
        return null; // Common ports and the shared persistence adapter are deliberately shared.
    }

    private static bool IsPublishedValue(string name) => name is
        "ServiceDesk.Domain.Ticketing.TicketCategory" or "ServiceDesk.Domain.Ticketing.Impact" or
        "ServiceDesk.Domain.Ticketing.Urgency" or "ServiceDesk.Domain.Identity.RoleNames";

    private static IEnumerable<TypeDefinition> Flatten(TypeDefinition type)
    {
        yield return type;
        foreach (var nested in type.NestedTypes.SelectMany(Flatten)) yield return nested;
    }

    private static IEnumerable<TypeReference> Expand(TypeReference type)
    {
        yield return type;
        if (type is TypeSpecification specification)
            foreach (var item in Expand(specification.ElementType)) yield return item;
        if (type is GenericInstanceType generic)
            foreach (var item in generic.GenericArguments.SelectMany(Expand)) yield return item;
    }

    private static IEnumerable<TypeReference> References(TypeDefinition type)
    {
        if (type.BaseType is not null) yield return type.BaseType;
        foreach (var item in type.Interfaces) yield return item.InterfaceType;
        foreach (var field in type.Fields) yield return field.FieldType;
        foreach (var method in type.Methods)
        {
            yield return method.ReturnType;
            foreach (var parameter in method.Parameters) yield return parameter.ParameterType;
            if (!method.HasBody) continue;
            foreach (var variable in method.Body.Variables) yield return variable.VariableType;
            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.Operand is TypeReference target) yield return target;
                if (instruction.Operand is MethodReference called)
                {
                    yield return called.DeclaringType; yield return called.ReturnType;
                    foreach (var parameter in called.Parameters) yield return parameter.ParameterType;
                    if (called is GenericInstanceMethod generic)
                        foreach (var argument in generic.GenericArguments) yield return argument;
                }
                if (instruction.Operand is FieldReference field)
                {
                    yield return field.DeclaringType; yield return field.FieldType;
                }
            }
        }
    }
}
