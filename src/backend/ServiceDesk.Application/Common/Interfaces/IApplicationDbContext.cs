namespace ServiceDesk.Application.Common.Interfaces;

/// <summary>
/// Application-owned abstraction over the database context.
/// Infrastructure provides the concrete EF Core implementation.
/// When domain entities are created, add their accessors here
/// using application-level types (not EF DbSet directly).
/// </summary>
public interface IApplicationDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
