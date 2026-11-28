using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Identity.Application.Departments;

public record CreateDepartmentCommand(string Code, string Name, string? Description);

public interface ICreateDepartment
{
    Task<DepartmentDto> ExecuteAsync(CreateDepartmentCommand command, CancellationToken cancellationToken = default);
}

internal sealed class CreateDepartmentUseCase(
    IDepartmentStore departmentStore,
    ICurrentUser currentUser,
    IClock clock) : ICreateDepartment
{
    public async Task<DepartmentDto> ExecuteAsync(CreateDepartmentCommand command, CancellationToken cancellationToken = default)
    {
        _ = AuthenticatedActor.RequireId(currentUser);
        if (!currentUser.IsInRole(RoleNames.Admin))
        {
            throw new UnauthorizedAccessException("Chỉ Quản trị viên (Admin) mới có quyền tạo phòng ban.");
        }

        ArgumentNullException.ThrowIfNull(command);

        var department = Department.Create(command.Code, command.Name, command.Description, clock.UtcNow.UtcDateTime);
        if (await departmentStore.ExistsCodeAsync(department.Code, cancellationToken))
        {
            throw new DepartmentConflictException($"Mã phòng ban '{command.Code.Trim().ToUpperInvariant()}' đã tồn tại.");
        }

        await departmentStore.AddAsync(department, cancellationToken);

        return new DepartmentDto(department.Id, department.Code, department.Name, department.Description);
    }
}
