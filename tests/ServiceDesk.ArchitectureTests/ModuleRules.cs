using Mono.Cecil;

namespace ServiceDesk.ArchitectureTests;

/// <summary>
/// Luật phụ thuộc cho kiến trúc "module trước, Clean bên trong module":
/// <list type="bullet">
/// <item>Mỗi module (ServiceDesk.Modules.{Module}) có các tầng Domain, Application, Infrastructure, Presentation
/// và một lớp điểm vào {Module}Module. Trong module, phụ thuộc chỉ đi vào trong:
/// Presentation → Application → Domain; Infrastructure → Application → Domain.</item>
/// <item>Giữa các module chỉ được dùng kiểu trong <see cref="Published"/>.</item>
/// <item>SharedKernel chỉ phụ thuộc SharedKernel; hạ tầng dùng chung (ServiceDesk.Infrastructure) không được
/// dùng Application/Presentation của module; host chỉ ghép module.</item>
/// </list>
/// </summary>
internal static class ModuleRules
{
    internal static readonly string[] Modules = ["Identity", "Ticketing", "Knowledge", "AIAssistance"];

    private static readonly string[] Layers = ["Domain", "Application", "Infrastructure", "Presentation"];

    internal static readonly HashSet<string> Published = new(StringComparer.Ordinal)
    {
        "ServiceDesk.Modules.Ticketing.Domain.TicketCategory", "ServiceDesk.Modules.Ticketing.Domain.Impact",
        "ServiceDesk.Modules.Ticketing.Domain.Urgency", "ServiceDesk.Modules.Identity.Application.RegisterUser.IRegisterUser",
        "ServiceDesk.Modules.Identity.Application.LoginUser.ILoginUser", "ServiceDesk.Modules.Identity.Application.GetCurrentUser.IGetCurrentUser",
        "ServiceDesk.Modules.Identity.Application.Contracts.RegisterRequest", "ServiceDesk.Modules.Identity.Application.Contracts.LoginRequest",
        "ServiceDesk.Modules.Identity.Application.Contracts.AuthResult", "ServiceDesk.Modules.Identity.Application.Contracts.UserInfo",
        "ServiceDesk.Modules.Ticketing.Application.CreateTicket.ICreateTicket", "ServiceDesk.Modules.Ticketing.Application.CreateTicket.CreateTicketCommand",
        "ServiceDesk.Modules.Ticketing.Application.GetMyTickets.IGetMyTickets", "ServiceDesk.Modules.Ticketing.Application.GetMyTickets.GetMyTicketsQuery",
        "ServiceDesk.Modules.Ticketing.Application.GetTicketDetail.IGetTicketDetail", "ServiceDesk.Modules.Ticketing.Application.GetTicketDetail.GetTicketDetailQuery",
        "ServiceDesk.Modules.Ticketing.Application.Contracts.TicketDto", "ServiceDesk.Modules.Ticketing.Application.Contracts.PagedTicketResult",
        "ServiceDesk.Modules.Ticketing.Application.UpdateTicket.IUpdateTicket", "ServiceDesk.Modules.Ticketing.Application.UpdateTicket.UpdateTicketCommand",
        "ServiceDesk.Modules.Ticketing.Application.ClaimTicket.IClaimTicket", "ServiceDesk.Modules.Ticketing.Application.ClaimTicket.ClaimTicketCommand",
        "ServiceDesk.Modules.Ticketing.Application.ResolveTicket.IResolveTicket", "ServiceDesk.Modules.Ticketing.Application.ResolveTicket.ResolveTicketCommand",
        "ServiceDesk.Modules.Ticketing.Application.ReopenTicket.IReopenTicket", "ServiceDesk.Modules.Ticketing.Application.ReopenTicket.ReopenTicketCommand",
        "ServiceDesk.Modules.Ticketing.Application.CloseTicket.ICloseTicket", "ServiceDesk.Modules.Ticketing.Application.CloseTicket.CloseTicketCommand",
        "ServiceDesk.Modules.Ticketing.Application.CancelTicket.ICancelTicket", "ServiceDesk.Modules.Ticketing.Application.CancelTicket.CancelTicketCommand",
        "ServiceDesk.Modules.Ticketing.Application.GetAgentQueue.IGetAgentQueue", "ServiceDesk.Modules.Ticketing.Application.GetAgentQueue.GetAgentQueueQuery",
        "ServiceDesk.Modules.Ticketing.Application.AssignTicket.IAssignTicket", "ServiceDesk.Modules.Ticketing.Application.AssignTicket.AssignTicketCommand",
        "ServiceDesk.Modules.Ticketing.Application.UnassignTicket.IUnassignTicket", "ServiceDesk.Modules.Ticketing.Application.UnassignTicket.UnassignTicketCommand",
        "ServiceDesk.Modules.Ticketing.Application.AddComment.IAddComment", "ServiceDesk.Modules.Ticketing.Application.AddComment.AddCommentCommand",
        "ServiceDesk.Modules.Ticketing.Application.AddInternalNote.IAddInternalNote", "ServiceDesk.Modules.Ticketing.Application.AddInternalNote.AddInternalNoteCommand",
        "ServiceDesk.Modules.Ticketing.Application.GetTicketTimeline.IGetTicketTimeline", "ServiceDesk.Modules.Ticketing.Application.GetTicketTimeline.GetTicketTimelineQuery",
        "ServiceDesk.Modules.Ticketing.Application.Contracts.TimelineItemDto", "ServiceDesk.Modules.Ticketing.Application.GenerateAttachmentUploadUrl.IGenerateAttachmentUploadUrl",
        "ServiceDesk.Modules.Ticketing.Application.GenerateAttachmentUploadUrl.GenerateAttachmentUploadUrlCommand", "ServiceDesk.Modules.Ticketing.Application.GetAttachmentDownloadUrl.IGetAttachmentDownloadUrl",
        "ServiceDesk.Modules.Ticketing.Application.GetAttachmentDownloadUrl.GetAttachmentDownloadUrlQuery", "ServiceDesk.Modules.Ticketing.Application.Contracts.GenerateUploadUrlRequest",
        "ServiceDesk.Modules.Ticketing.Application.Contracts.UploadUrlResult", "ServiceDesk.Modules.Ticketing.Application.Contracts.DownloadUrlResult",
        "ServiceDesk.Modules.Ticketing.Application.ConfirmAttachmentUpload.IConfirmAttachmentUpload", "ServiceDesk.Modules.Ticketing.Application.ConfirmAttachmentUpload.ConfirmAttachmentUploadCommand",
        "ServiceDesk.Modules.Ticketing.Application.GetTicketAttachments.IGetTicketAttachments", "ServiceDesk.Modules.Ticketing.Application.GetTicketAttachments.GetTicketAttachmentsQuery",
        "ServiceDesk.Modules.Ticketing.Application.Contracts.ConfirmAttachmentUploadRequest", "ServiceDesk.Modules.Ticketing.Application.Contracts.TicketAttachmentDto",
        "ServiceDesk.Modules.Identity.Application.GetUsersByRole.IGetUsersByRole", "ServiceDesk.Modules.Identity.Application.GetUsersByRole.GetUsersByRoleQuery",
        "ServiceDesk.Modules.Identity.Application.GetUsersByRole.UserDirectoryEntry", "ServiceDesk.Modules.Ticketing.Application.Idempotency.IdempotencyConflictException",
        "ServiceDesk.Modules.Ticketing.Application.Idempotency.IdempotencyInProgressException", "ServiceDesk.Modules.Knowledge.Domain.ArticleStatus",
        "ServiceDesk.Modules.Knowledge.Application.Contracts.ArticleDto", "ServiceDesk.Modules.Knowledge.Application.Contracts.ArticleDraftDto",
        "ServiceDesk.Modules.Knowledge.Application.Contracts.PagedArticleResult", "ServiceDesk.Modules.Knowledge.Application.CreateDraftArticle.ICreateDraftArticle",
        "ServiceDesk.Modules.Knowledge.Application.CreateDraftArticle.CreateDraftArticleCommand", "ServiceDesk.Modules.Knowledge.Application.UpdateDraftArticle.IUpdateDraftArticle",
        "ServiceDesk.Modules.Knowledge.Application.UpdateDraftArticle.UpdateDraftArticleCommand", "ServiceDesk.Modules.Knowledge.Application.PublishArticle.IPublishArticle",
        "ServiceDesk.Modules.Knowledge.Application.PublishArticle.PublishArticleCommand", "ServiceDesk.Modules.Knowledge.Application.ArchiveArticle.IArchiveArticle",
        "ServiceDesk.Modules.Knowledge.Application.ArchiveArticle.ArchiveArticleCommand", "ServiceDesk.Modules.Knowledge.Application.GetArticleDetail.IGetArticleDetail",
        "ServiceDesk.Modules.Knowledge.Application.GetArticleDetail.GetArticleDetailQuery", "ServiceDesk.Modules.Knowledge.Application.SearchKnowledge.ISearchKnowledge",
        "ServiceDesk.Modules.Knowledge.Application.SearchKnowledge.SearchKnowledgeQuery"
    };

    /// <summary>Kiểu Ticketing mà AIAssistance được phép dùng (giá trị phân loại, không phải aggregate Ticket).</summary>
    private static readonly HashSet<string> AiAllowedTicketingTypes = new(StringComparer.Ordinal)
    {
        "ServiceDesk.Modules.Ticketing.Domain.TicketCategory", "ServiceDesk.Modules.Ticketing.Domain.Impact",
        "ServiceDesk.Modules.Ticketing.Domain.Urgency"
    };

    private static readonly HashSet<string> Composition = new(StringComparer.Ordinal)
    {
        "ServiceDesk.Infrastructure.DependencyInjection", "ServiceDesk.Worker.WorkerServices", "ServiceDesk.Api.Program", "Program"
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
        if (name.StartsWith("ServiceDesk.Infrastructure.", StringComparison.Ordinal)) return new("Platform");
        if (name.StartsWith("ServiceDesk.Api.Endpoints.", StringComparison.Ordinal)) return new("HostPresentation");
        if (name.StartsWith("ServiceDesk.Api.Identity.", StringComparison.Ordinal)) return new("HostAdapter");
        foreach (var module in Modules)
        {
            var prefix = $"ServiceDesk.Modules.{module}.";
            if (!name.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var rest = name[prefix.Length..];
            if (!rest.Contains('.', StringComparison.Ordinal)) return new("Module", module, "Module");
            var layer = rest[..rest.IndexOf('.', StringComparison.Ordinal)];
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
                // Host không phải lối tắt để truy cập database: chỉ được dùng DI và Options của hạ tầng dùng chung.
                if (other.Kind == "Platform" && assembly is "ServiceDesk.Api" or "ServiceDesk.Worker")
                    return name == "ServiceDesk.Infrastructure.DependencyInjection" ||
                           name.StartsWith("ServiceDesk.Infrastructure.Options.", StringComparison.Ordinal);
                return true;
            case "Shared":
                return other.Kind == "Shared";
            case "SharedWeb":
                return other.Kind is "Shared" or "SharedWeb";
            case "HostAdapter":
                return other.Kind is "Shared" or "HostAdapter";
            case "HostPresentation":
                return other.Kind is "Shared" or "SharedWeb" or "HostPresentation" || Published.Contains(name);
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
        {
            if (source.Module == "AIAssistance" && other.Module == "Ticketing") return AiAllowedTicketingTypes.Contains(name);
            if (!Published.Contains(name)) return false;
            // Domain chỉ được dùng kiểu Domain đã công bố của module khác.
            return source.Layer != "Domain" || other.Layer == "Domain";
        }
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
