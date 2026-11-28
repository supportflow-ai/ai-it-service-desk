using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Identity.Application.Departments;

public record UpdateDepartmentCommand(Guid Id, string Name, string? Description);

public interface IUpdateDepartment
{
    Task<DepartmentDto> ExecuteAsync(UpdateDepartmentCommand command, CancellationToken cancellationToken = default);
}

internal sealed class UpdateDepartmentUseCase(
    IDepartmentStore departmentStore,
    ICurrentUser currentUser) : IUpdateDepartment
{
    public async Task<DepartmentDto> ExecuteAsync(UpdateDepartmentCommand command, CancellationToken cancellationToken = default)
    {
        _ = AuthenticatedActor.RequireId(currentUser);
        if (!currentUser.IsInRole(RoleNames.Admin))
        {
            throw new UnauthorizedAccessException("Chỉ Quản trị viên (Admin) mới có quyền cập nhật phòng ban.");
        }

        ArgumentNullException.ThrowIfNull(command);

        var department = await departmentStore.GetByIdAsync(command.Id, cancellationToken);
        if (department is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy phòng ban có mã {command.Id}.");
        }

        department.Update(command.Name, command.Description);
        await departmentStore.UpdateAsync(department, cancellationToken);

        return new DepartmentDto(department.Id, department.Code, department.Name, department.Description);
    }
}
