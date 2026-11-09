using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Ports;

namespace ServiceDesk.Modules.Identity.Application.GetDepartments;

public sealed record GetDepartmentsQuery();

public interface IGetDepartments
{
    Task<IReadOnlyList<DepartmentDto>> ExecuteAsync(GetDepartmentsQuery query, CancellationToken cancellationToken = default);
}

internal sealed class GetDepartmentsUseCase(IDepartmentQueries queries) : IGetDepartments
{
    public Task<IReadOnlyList<DepartmentDto>> ExecuteAsync(GetDepartmentsQuery query, CancellationToken cancellationToken = default)
    {
        return queries.GetAllAsync(cancellationToken);
    }
}
