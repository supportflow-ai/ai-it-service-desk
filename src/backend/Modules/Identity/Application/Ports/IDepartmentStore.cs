using ServiceDesk.Modules.Identity.Domain;

namespace ServiceDesk.Modules.Identity.Application.Ports;

public interface IDepartmentStore
{
    Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsCodeAsync(string code, CancellationToken cancellationToken = default);
    Task AddAsync(Department department, CancellationToken cancellationToken = default);
    Task UpdateAsync(Department department, CancellationToken cancellationToken = default);
}
