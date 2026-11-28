using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using ServiceDesk.Modules.Identity.Application.Departments;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Domain;

namespace ServiceDesk.Modules.Identity.Infrastructure;

internal sealed class PostgresDepartmentStore(DbContext context) : IDepartmentStore
{
    public Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Set<Department>().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<bool> ExistsCodeAsync(string code, CancellationToken cancellationToken = default) =>
        context.Set<Department>().AnyAsync(d => d.Code == code.Trim().ToUpperInvariant(), cancellationToken);

    public async Task AddAsync(Department department, CancellationToken cancellationToken = default)
    {
        await context.Set<Department>().AddAsync(department, cancellationToken);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is DbException { SqlState: "23505" })
        {
            throw new DepartmentConflictException("Phòng ban đã tồn tại.");
        }
    }

    public async Task UpdateAsync(Department department, CancellationToken cancellationToken = default)
    {
        context.Set<Department>().Update(department);
        await context.SaveChangesAsync(cancellationToken);
    }
}
