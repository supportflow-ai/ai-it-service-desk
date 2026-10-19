using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace ServiceDesk.ArchitectureTests;

public sealed class ModuleRuleNegativeTests
{
    [Theory]
    [InlineData("ServiceDesk.Application.Common", "Helper", "ServiceDesk.Domain.Ticketing", "Ticket")]
    [InlineData("ServiceDesk.Api.Other", "Helper", "ServiceDesk.Infrastructure.Persistence", "ApplicationDbContext")]
    [InlineData("ServiceDesk.Api.Endpoints", "Helper", "ServiceDesk.Infrastructure.Persistence", "ApplicationDbContext")]
    [InlineData("ServiceDesk.Api.Identity", "Helper", "Microsoft.EntityFrameworkCore", "DbContext")]
    [InlineData("ServiceDesk.Application.Identity", "Helper", "ServiceDesk.Infrastructure.Ticketing", "PostgresTicketWriter")]
    [InlineData("ServiceDesk.Application.Unregistered", "Feature", "System", "String")]
    [InlineData("ServiceDesk.Application.AIAssistance", "Helper", "ServiceDesk.Application.Ticketing.Contracts", "TicketDto")]
    [InlineData("ServiceDesk.Application.AIAssistance", "Helper", "ServiceDesk.Domain.Identity", "RoleNames")]
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
        using var module = ModuleDefinition.CreateModule("ServiceDesk.Application", ModuleKind.Dll);
        var helper = new TypeDefinition("ServiceDesk.Application.Common", "Helper", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(helper);
        var method = new MethodDefinition("Read", MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
        helper.Methods.Add(method);
        var set = new MethodReference("Set", module.TypeSystem.Object,
            new TypeReference("Microsoft.EntityFrameworkCore", "DbContext", module, module));
        set.GenericParameters.Add(new GenericParameter("T", set));
        var call = new GenericInstanceMethod(set);
        call.GenericArguments.Add(new TypeReference("ServiceDesk.Domain.Ticketing", "Ticket", module, module));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Call, call));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Pop));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        Assert.Contains(ModuleRules.Check([helper]), e => e.Contains("ServiceDesk.Domain.Ticketing.Ticket", StringComparison.Ordinal));
    }
}
