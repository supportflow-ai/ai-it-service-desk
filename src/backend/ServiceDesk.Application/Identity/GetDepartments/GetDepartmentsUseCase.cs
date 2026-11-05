using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Identity.Ports;

namespace ServiceDesk.Application.Identity.GetDepartments;

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
