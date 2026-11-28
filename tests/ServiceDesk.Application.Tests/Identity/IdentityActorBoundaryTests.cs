using Microsoft.Extensions.DependencyInjection;
using Moq;
using ServiceDesk.Modules.Identity.Application.AssignUserRoles;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Departments;
using ServiceDesk.Modules.Identity.Application.GetDepartments;
using ServiceDesk.Modules.Identity.Application.GetUsersByRole;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.Application.Tests.Identity;

public sealed class IdentityActorBoundaryTests
{
    public static IEnumerable<object?[]> InvalidActors()
    {
        foreach (var operation in new[] { "roles", "users", "create-department", "update-department", "departments" })
        {
            yield return [operation, false, Guid.NewGuid().ToString()];
            foreach (var id in new[] { null, "", "invalid", Guid.Empty.ToString() })
                yield return [operation, true, id];
        }
    }

    [Theory]
    [MemberData(nameof(InvalidActors))]
    public async Task ProtectedUseCasesRejectInvalidActorsBeforePersistence(string operation, bool authenticated, string? id)
    {
        var actor = new Mock<ICurrentUser>();
        actor.SetupGet(u => u.IsAuthenticated).Returns(authenticated);
        actor.SetupGet(u => u.UserId).Returns(id);
        actor.Setup(u => u.IsInRole(It.IsAny<string>())).Returns(true);
        var accounts = new Mock<IUserAccountGateway>();
        accounts.Setup(a => a.AssignRolesAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Success());
        accounts.Setup(a => a.GetUsersInRoleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var store = new Mock<IDepartmentStore>();
        store.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Department.Create("IT", "IT", null, DateTime.UtcNow));
        var queries = new Mock<IDepartmentQueries>();
        queries.Setup(q => q.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        using var services = new ServiceCollection().AddSingleton(actor.Object).BuildServiceProvider();
        Func<Task> run = operation switch
        {
            "roles" => () => new AssignUserRolesUseCase(accounts.Object, actor.Object, clock.Object)
                .ExecuteAsync(Guid.NewGuid(), new AssignRolesRequest([RoleNames.Agent])),
            "users" => () => new GetUsersByRoleUseCase(accounts.Object, actor.Object).ExecuteAsync(new(RoleNames.Agent)),
            "create-department" => () => new CreateDepartmentUseCase(store.Object, actor.Object, clock.Object).ExecuteAsync(new("IT", "IT", null)),
            "update-department" => () => new UpdateDepartmentUseCase(store.Object, actor.Object).ExecuteAsync(new(Guid.NewGuid(), "IT", null)),
            _ => () => ActivatorUtilities.CreateInstance<GetDepartmentsUseCase>(services, queries.Object).ExecuteAsync(new())
        };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(run);
        accounts.VerifyNoOtherCalls();
        store.VerifyNoOtherCalls();
        queries.VerifyNoOtherCalls();
    }
}
