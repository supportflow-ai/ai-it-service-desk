using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Identity.Ports;
using ServiceDesk.Infrastructure.Persistence;

namespace ServiceDesk.Infrastructure.Identity;

internal sealed class PostgresDepartmentQueries(ApplicationDbContext context) : IDepartmentQueries
{
    public async Task<IReadOnlyList<DepartmentDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.Departments
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentDto(d.Id, d.Code, d.Name, d.Description))
            .ToListAsync(cancellationToken);
    }
}
