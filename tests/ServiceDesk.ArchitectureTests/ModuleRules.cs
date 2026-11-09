using Mono.Cecil;

namespace ServiceDesk.ArchitectureTests;

/// <summary>
/// Luật phụ thuộc cho kiến trúc "module trước, Clean bên trong module":
/// <list type="bullet">
/// <item>Mỗi module (ServiceDesk.Modules.{Module}) có các tầng Domain, Application, Infrastructure, Presentation
/// và một lớp điểm vào {Module}Module. Trong module, phụ thuộc chỉ đi vào trong:
/// Presentation → Application → Domain; Infrastructure → Application → Domain.</item>
/// <item>Giữa các module chỉ được dùng kiểu trong <see cref="Published"/>.</item>
/// <item>SharedKernel chỉ phụ thuộc SharedKernel; hạ tầng dùng chung (ServiceDesk.Platform) không được
/// dùng Application/Presentation của module; host chỉ ghép module.</item>
/// </list>
/// </summary>
internal static class ModuleRules
{
    internal static readonly string[] Modules = ["Identity", "Ticketing", "Knowledge", "AIAssistance"];

    private static readonly string[] Layers = ["Domain", "Application", "Infrastructure", "Presentation"];

    /// <summary>Kiểu Ticketing mà AIAssistance được phép dùng (giá trị phân loại, không phải aggregate Ticket).</summary>
    internal static readonly HashSet<string> Published = new(StringComparer.Ordinal)
    {
        "ServiceDesk.Modules.Ticketing.Domain.TicketCategory", "ServiceDesk.Modules.Ticketing.Domain.Impact",
        "ServiceDesk.Modules.Ticketing.Domain.Urgency"
    };

    private static readonly HashSet<string> Composition = new(StringComparer.Ordinal)
    {
        "ServiceDesk.Platform.DependencyInjection", "ServiceDesk.Worker.WorkerServices", "ServiceDesk.Api.Program", "Program"
    };

    private static readonly string[] PersistenceFrameworks = ["Microsoft.EntityFrameworkCore.", "Npgsql."];

    internal sealed record Owner(string Kind, string? Module = null, string? Layer = null);

    internal static string RootName(TypeReference type) =>
        type.DeclaringType is null ? type.FullName : RootName(type.DeclaringType);

    internal static Owner? Classify(TypeReference type)
    {
        var name = RootName(type);
        if (Composition.Contains(name)) return new("Composition");
        if (name.StartsWith("<>f__Anonymous", StringComparison.Ordinal) || name.StartsWith("<>z__", StringComparison.Ordinal) ||
            name.StartsWith("<>y__", StringComparison.Ordinal) ||
            name.StartsWith("<PrivateImplementationDetails>", StringComparison.Ordinal))
            return new("Generated");
        if (name.StartsWith("ServiceDesk.SharedKernel.Web.", StringComparison.Ordinal)) return new("SharedWeb");
        if (name.StartsWith("ServiceDesk.SharedKernel.", StringComparison.Ordinal)) return new("Shared");
        if (name.StartsWith("ServiceDesk.Platform.", StringComparison.Ordinal)) return new("Platform");
        if (name.StartsWith("ServiceDesk.Api.Endpoints.", StringComparison.Ordinal)) return new("HostPresentation");
        if (name.StartsWith("ServiceDesk.Api.Authentication.", StringComparison.Ordinal)) return new("HostAdapter");
        foreach (var module in Modules)
        {
            var prefix = $"ServiceDesk.Modules.{module}.";
            if (!name.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var rest = name[prefix.Length..];
            var separator = rest.IndexOf('.', StringComparison.Ordinal);
            if (separator < 0) return rest == $"{module}Module" ? new("Module", module, "Module") : null;
            var layer = rest[..separator];
            return Layers.Contains(layer) ? new("Module", module, layer) : null;
        }
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
            var assembly = type.Module.Assembly.Name.Name;
            foreach (var target in References(type).SelectMany(Expand))
            {
                var name = RootName(target);
                var external = ExternalViolation(source, assembly, name);
                if (external is not null) errors.Add($"{external}: {type.FullName} -> {name}");
                if (!name.StartsWith("ServiceDesk.", StringComparison.Ordinal)) continue;
                var other = Classify(target);
                if (other is null) { errors.Add($"Unclassified dependency: {type.FullName} -> {name}"); continue; }
                if (!Allowed(source, assembly, other, name)) errors.Add($"Forbidden dependency: {type.FullName} -> {target.FullName}");
            }
        }
        return errors.Distinct().ToArray();
    }

    /// <summary>Thư viện framework bị cấm theo vị trí của kiểu nguồn.</summary>
    private static string? ExternalViolation(Owner source, string assembly, string name)
    {
        var persistence = PersistenceFrameworks.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
        var web = name.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal);
        if (persistence && (assembly == "ServiceDesk.Api" || source.Kind is "Shared" or "SharedWeb" or "HostPresentation" or "HostAdapter" ||
                            source.Layer is "Domain" or "Application" or "Presentation"))
            return "Persistence access outside a module adapter";
        if (web && (source.Kind == "Shared" || source.Layer is "Domain" or "Application"))
            return "Web framework access from Domain/Application/SharedKernel";
        return null;
    }

    private static bool Allowed(Owner source, string assembly, Owner other, string name)
    {
        if (other.Kind == "Generated") return true;
        switch (source.Kind)
        {
            case "Generated":
                return true;
            case "Composition":
                if (assembly is "ServiceDesk.Api" or "ServiceDesk.Worker")
                {
                    // Điểm đăng ký DependencyInjection được phân loại là Composition.
                    if (other.Kind == "Platform") return false;
                    if (other.Kind == "Module")
                        return other.Layer == "Module" || (assembly == "ServiceDesk.Api" && other.Layer == "Presentation");
                }
                return true;
            case "Shared":
                return other.Kind == "Shared";
            case "SharedWeb":
                return other.Kind is "Shared" or "SharedWeb";
            case "HostAdapter":
                return other.Kind is "Shared" or "HostAdapter";
            case "HostPresentation":
                return other.Kind is "Shared" or "SharedWeb" or "HostPresentation";
            case "Platform":
                // DbContext, migration và bảo trì database cần thực thể, cấu hình ánh xạ và điểm vào module;
                // không bao giờ dùng use case hay endpoint.
                return other.Kind is "Shared" or "Platform" ||
                       (other.Kind == "Module" && other.Layer is "Domain" or "Infrastructure" or "Module");
            case "Module":
                return ModuleAllowed(source, other, name);
            default:
                return false;
        }
    }

    private static bool ModuleAllowed(Owner source, Owner other, string name)
    {
        if (source.Module == "AIAssistance" && name.StartsWith("ServiceDesk.SharedKernel.Authorization.", StringComparison.Ordinal))
            return false;
        if (other.Kind == "Shared") return true;
        if (other.Kind == "SharedWeb") return source.Layer is "Presentation" or "Module";
        if (other.Kind != "Module") return false;
        if (other.Module != source.Module)
            return source.Module == "AIAssistance" && other.Module == "Ticketing" && Published.Contains(name);
        return source.Layer switch
        {
            "Module" => true,
            "Domain" => other.Layer == "Domain",
            "Application" => other.Layer is "Domain" or "Application",
            "Infrastructure" => other.Layer is "Domain" or "Application" or "Infrastructure",
            "Presentation" => other.Layer is "Domain" or "Application" or "Presentation",
            _ => false
        };
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
