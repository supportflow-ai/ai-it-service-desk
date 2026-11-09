using ServiceDesk.Modules.Identity.Application.Contracts;

namespace ServiceDesk.Modules.Identity.Application.Ports;

public interface IDepartmentQueries
{
    Task<IReadOnlyList<DepartmentDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
