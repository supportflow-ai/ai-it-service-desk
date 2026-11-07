using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Domain;

namespace ServiceDesk.Modules.Identity.Infrastructure;

internal sealed class PostgresDepartmentQueries(DbContext context) : IDepartmentQueries
{
    public async Task<IReadOnlyList<DepartmentDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.Set<Department>()
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentDto(d.Id, d.Code, d.Name, d.Description))
            .ToListAsync(cancellationToken);
    }
}
