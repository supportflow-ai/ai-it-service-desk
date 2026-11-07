using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace ServiceDesk.ArchitectureTests;

public sealed class ModuleRuleNegativeTests
{
    [Theory]
    // SharedKernel không được phụ thuộc module.
    [InlineData("ServiceDesk.SharedKernel.Application", "Helper", "ServiceDesk.Modules.Ticketing.Domain", "Ticket")]
    // Kiểu chưa phân loại trong host.
    [InlineData("ServiceDesk.Api.Other", "Helper", "ServiceDesk.Infrastructure.Persistence", "ApplicationDbContext")]
    // Endpoint của host không được chạm DbContext.
    [InlineData("ServiceDesk.Api.Endpoints", "Helper", "ServiceDesk.Infrastructure.Persistence", "ApplicationDbContext")]
    [InlineData("ServiceDesk.Api.Identity", "Helper", "Microsoft.EntityFrameworkCore", "DbContext")]
    // Module không được dùng phần chưa công bố của module khác.
    [InlineData("ServiceDesk.Modules.Identity.Application", "Helper", "ServiceDesk.Modules.Ticketing.Infrastructure", "PostgresTicketWriter")]
    // Module không có trong danh sách.
    [InlineData("ServiceDesk.Modules.Unregistered.Application", "Feature", "System", "String")]
    // Tầng không có trong danh sách.
    [InlineData("ServiceDesk.Modules.Ticketing.Helpers", "Feature", "System", "String")]
    // AIAssistance chỉ dùng giá trị phân loại của Ticketing, không dùng DTO hay vai trò.
    [InlineData("ServiceDesk.Modules.AIAssistance.Application", "Helper", "ServiceDesk.Modules.Ticketing.Application.Contracts", "TicketDto")]
    [InlineData("ServiceDesk.Modules.AIAssistance.Application", "Helper", "ServiceDesk.SharedKernel.Authorization", "RoleNames")]
    // Luật Clean bên trong module: phụ thuộc chỉ đi vào trong.
    [InlineData("ServiceDesk.Modules.Ticketing.Domain", "Helper", "ServiceDesk.Modules.Ticketing.Application.Ports", "ITicketWriter")]
    [InlineData("ServiceDesk.Modules.Ticketing.Application", "Helper", "ServiceDesk.Modules.Ticketing.Infrastructure", "PostgresTicketWriter")]
    [InlineData("ServiceDesk.Modules.Ticketing.Application", "Helper", "ServiceDesk.Modules.Ticketing.Presentation", "TicketEndpoints")]
    [InlineData("ServiceDesk.Modules.Knowledge.Presentation", "Helper", "ServiceDesk.Modules.Knowledge.Infrastructure", "PostgresKnowledgeWriter")]
    [InlineData("ServiceDesk.Modules.Ticketing.Domain", "Helper", "Microsoft.EntityFrameworkCore", "DbContext")]
    [InlineData("ServiceDesk.Modules.Ticketing.Application", "Helper", "Microsoft.AspNetCore.Http", "HttpContext")]
    // Hạ tầng dùng chung không được gọi use case.
    [InlineData("ServiceDesk.Infrastructure.Persistence", "Helper", "ServiceDesk.Modules.Ticketing.Application.CreateTicket", "ICreateTicket")]
    public void ForbiddenOrUnclassifiedTypesAreDetected(string sourceNamespace, string sourceName, string targetNamespace, string targetName)
    {
        using var module = ModuleDefinition.CreateModule("ServiceDesk.Api", ModuleKind.Dll);
        var source = new TypeDefinition(sourceNamespace, sourceName, TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(source);
        source.Fields.Add(new FieldDefinition("dependency", FieldAttributes.Private,
            new TypeReference(targetNamespace, targetName, module, module)));
        Assert.NotEmpty(ModuleRules.Check([source]));
    }

    [Fact]
    public void GenericSetAccessCannotHideAnEntityDependency()
    {
        using var module = ModuleDefinition.CreateModule("ServiceDesk.SharedKernel", ModuleKind.Dll);
        var helper = new TypeDefinition("ServiceDesk.SharedKernel.Application", "Helper", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(helper);
        var method = new MethodDefinition("Read", MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
        helper.Methods.Add(method);
        var set = new MethodReference("Set", module.TypeSystem.Object,
            new TypeReference("Microsoft.EntityFrameworkCore", "DbContext", module, module));
        set.GenericParameters.Add(new GenericParameter("T", set));
        var call = new GenericInstanceMethod(set);
        call.GenericArguments.Add(new TypeReference("ServiceDesk.Modules.Ticketing.Domain", "Ticket", module, module));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Call, call));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Pop));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        Assert.Contains(ModuleRules.Check([helper]), e => e.Contains("ServiceDesk.Modules.Ticketing.Domain.Ticket", StringComparison.Ordinal));
    }

    [Theory]
    // Các phụ thuộc hợp lệ không bị báo nhầm.
    [InlineData("ServiceDesk.Modules.Ticketing.Infrastructure", "Helper", "ServiceDesk.Modules.Ticketing.Domain", "Ticket")]
    [InlineData("ServiceDesk.Modules.Ticketing.Application", "Helper", "ServiceDesk.SharedKernel.Application.Abstractions", "IClock")]
    [InlineData("ServiceDesk.Modules.AIAssistance.Domain", "Helper", "ServiceDesk.Modules.Ticketing.Domain", "TicketCategory")]
    [InlineData("ServiceDesk.Modules.Knowledge.Presentation", "Helper", "ServiceDesk.SharedKernel.Web", "LegacyErrorFilter")]
    [InlineData("ServiceDesk.Infrastructure.Persistence", "Helper", "ServiceDesk.Modules.Ticketing.Domain", "Ticket")]
    public void AllowedDependenciesAreAccepted(string sourceNamespace, string sourceName, string targetNamespace, string targetName)
    {
        using var module = ModuleDefinition.CreateModule("ServiceDesk.Modules.Test", ModuleKind.Dll);
        var source = new TypeDefinition(sourceNamespace, sourceName, TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(source);
        source.Fields.Add(new FieldDefinition("dependency", FieldAttributes.Private,
            new TypeReference(targetNamespace, targetName, module, module)));
        Assert.Empty(ModuleRules.Check([source]));
    }
}
