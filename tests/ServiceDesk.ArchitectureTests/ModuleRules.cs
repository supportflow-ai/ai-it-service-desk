using Mono.Cecil;
using Mono.Cecil.Cil;
using ServiceDesk.ArchitectureTests.Rules;

namespace ServiceDesk.ArchitectureTests;

internal static class ModuleRules
{
    internal static IReadOnlySet<string> Published => PublishedContracts.Presentation;

    private static readonly HashSet<string> Composition = new(StringComparer.Ordinal)
    {
        "ServiceDesk.Application.DependencyInjection", "ServiceDesk.Infrastructure.DependencyInjection",
        "ServiceDesk.Worker.WorkerServices", "ServiceDesk.Api.Program", "Program"
    };

    private static readonly HashSet<string> TechnicalExceptions = new(StringComparer.Ordinal)
    {
        "ServiceDesk.Infrastructure.Persistence.ApplicationDbContext",
        "ServiceDesk.Infrastructure.Persistence.DatabaseMaintenance",
        "ServiceDesk.Infrastructure.Persistence.DatabaseMaintenanceException",
        "ServiceDesk.Infrastructure.Persistence.Migrations.AddTicketTable",
        "ServiceDesk.Infrastructure.Persistence.Migrations.AddTicketIdempotency",
        "ServiceDesk.Infrastructure.Persistence.Migrations.AddRequesterCreatedAtIdIndex",
        "ServiceDesk.Infrastructure.Persistence.Migrations.RestoreIdentitySchema",
        "ServiceDesk.Infrastructure.Persistence.Migrations.FixTicketNumberWidth",
        "ServiceDesk.Infrastructure.Persistence.Migrations.ApplicationDbContextModelSnapshot"
    };

    internal static string RootName(TypeReference type) =>
        type.DeclaringType is null ? type.FullName : RootName(type.DeclaringType);

    private static string? Classify(TypeReference type)
    {
        var name = RootName(type);
        if (Composition.Contains(name)) return "Composition";
        if (TechnicalExceptions.Contains(name)) return "Provisioning";
        if (name.StartsWith("<>f__AnonymousType", StringComparison.Ordinal)) return "Presentation";
        if (name.StartsWith("<>f__AnonymousDelegate", StringComparison.Ordinal)) return "Presentation";
        if (name.StartsWith("<>z__ReadOnlyArray", StringComparison.Ordinal) ||
            name.StartsWith("<>z__ReadOnlySingleElementList", StringComparison.Ordinal)) return "Shared";
        foreach (var layer in new[] { "Domain", "Application", "Infrastructure" })
            foreach (var module in new[] { "Identity", "Ticketing", "AIAssistance", "AI" })
                if (name.StartsWith($"ServiceDesk.{layer}.{module}.", StringComparison.Ordinal))
                    return module == "AI" ? "AIAssistance" : module;
        foreach (var prefix in new[] { "ServiceDesk.Domain.Common.", "ServiceDesk.Application.Common.",
                     "ServiceDesk.Infrastructure.Clock.", "ServiceDesk.Infrastructure.Storage.", "ServiceDesk.Infrastructure.Options.",
                     "ServiceDesk.Api.Identity." })
            if (name.StartsWith(prefix, StringComparison.Ordinal)) return "Shared";
        if (name.StartsWith("ServiceDesk.Api.Endpoints.", StringComparison.Ordinal)) return "Presentation";
        return null;
    }

    internal static IReadOnlyList<string> Check(IEnumerable<TypeDefinition> types)
    {
        var errors = new List<string>();
        foreach (var type in types)
        {
            if (type.Name == "<Module>") continue;
            var source = Classify(type);
            if (source is null) { errors.Add($"Unclassified production type: {type.FullName}"); continue; }
            foreach (var target in References(type).SelectMany(Expand))
            {
                var name = RootName(target);
                if ((source == "Shared" || type.Module.Assembly.Name.Name == "ServiceDesk.Api") &&
                    (name.StartsWith("Microsoft.EntityFrameworkCore.", StringComparison.Ordinal) ||
                     name.StartsWith("Npgsql.", StringComparison.Ordinal)))
                    errors.Add($"Persistence access outside a module adapter: {type.FullName} -> {name}");
                // Các kiểu Identity của framework cũng thuộc quyền sở hữu mô-đun Identity.
                if (name.StartsWith("Microsoft.AspNetCore.Identity.", StringComparison.Ordinal) &&
                    source is not ("Identity" or "Provisioning" or "Composition"))
                    errors.Add($"Identity persistence access outside its owning module: {type.FullName} -> {name}");
                if (!name.StartsWith("ServiceDesk.", StringComparison.Ordinal)) continue;
                var other = Classify(target);
                if (other is null) { errors.Add($"Unclassified dependency: {type.FullName} -> {name}"); continue; }
                var published = Published.Contains(name);
                var allowed = source switch
                {
                    "Composition" => true,
                    "Provisioning" => other is "Provisioning" or "Shared" or "Ticketing" or "Identity",
                    "Shared" => other == "Shared",
                    "Presentation" => other is "Presentation" or "Shared" || published,
                    _ => other == source || other == "Shared" || PublishedContracts.Allows(source, other, name) ||
                         (name == "ServiceDesk.Infrastructure.Persistence.ApplicationDbContext" &&
                          RootName(type).StartsWith("ServiceDesk.Infrastructure.", StringComparison.Ordinal))
                };
                // Composition is not an escape hatch for arbitrary API/Worker database access.
                if (source == "Composition" && (type.Module.Assembly.Name.Name is "ServiceDesk.Api" or "ServiceDesk.Worker") &&
                    name.StartsWith("ServiceDesk.Infrastructure.", StringComparison.Ordinal))
                    allowed = name == "ServiceDesk.Infrastructure.DependencyInjection" ||
                              name.StartsWith("ServiceDesk.Infrastructure.Options.", StringComparison.Ordinal);
                if (!allowed) errors.Add($"Forbidden dependency: {type.FullName} -> {target.FullName}");
            }
        }
        return errors.Distinct().ToArray();
    }

    internal static IEnumerable<TypeDefinition> Flatten(TypeDefinition type)
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
        foreach (var parameter in type.GenericParameters)
            foreach (var constraint in parameter.Constraints) yield return constraint.ConstraintType;
        foreach (var field in type.Fields) yield return field.FieldType;
        foreach (var property in type.Properties) yield return property.PropertyType;
        foreach (var attribute in type.CustomAttributes) yield return attribute.AttributeType;
        foreach (var method in type.Methods)
        {
            yield return method.ReturnType;
            foreach (var parameter in method.Parameters) yield return parameter.ParameterType;
            foreach (var parameter in method.GenericParameters)
                foreach (var constraint in parameter.Constraints) yield return constraint.ConstraintType;
            if (!method.HasBody) continue;
            foreach (var variable in method.Body.Variables) yield return variable.VariableType;
            foreach (var handler in method.Body.ExceptionHandlers)
                if (handler.CatchType is not null) yield return handler.CatchType;
            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.Operand is TypeReference target) yield return target;
                if (instruction.Operand is MethodReference called)
                {
                    yield return called.DeclaringType;
                    yield return called.ReturnType;
                    foreach (var parameter in called.Parameters) yield return parameter.ParameterType;
                    if (called is GenericInstanceMethod generic)
                        foreach (var argument in generic.GenericArguments) yield return argument;
                }
                if (instruction.Operand is FieldReference field)
                {
                    yield return field.DeclaringType;
                    yield return field.FieldType;
                }
            }
        }
    }
}
