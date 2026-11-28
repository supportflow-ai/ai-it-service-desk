using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.Identity.Application.GetDepartments;

public sealed record GetDepartmentsQuery();

public interface IGetDepartments
{
    Task<IReadOnlyList<DepartmentDto>> ExecuteAsync(GetDepartmentsQuery query, CancellationToken cancellationToken = default);
}

internal sealed class GetDepartmentsUseCase(IDepartmentQueries queries, ICurrentUser currentUser) : IGetDepartments
{
    public Task<IReadOnlyList<DepartmentDto>> ExecuteAsync(GetDepartmentsQuery query, CancellationToken cancellationToken = default)
    {
        _ = AuthenticatedActor.RequireId(currentUser);
        return queries.GetAllAsync(cancellationToken);
    }
}
