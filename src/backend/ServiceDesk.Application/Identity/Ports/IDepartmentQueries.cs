using ServiceDesk.Application.Identity.Contracts;

namespace ServiceDesk.Application.Identity.Ports;

public interface IDepartmentQueries
{
    Task<IReadOnlyList<DepartmentDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
