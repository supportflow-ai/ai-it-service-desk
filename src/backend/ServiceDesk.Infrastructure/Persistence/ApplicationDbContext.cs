using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Common.Interfaces;

namespace ServiceDesk.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext — the single database context for the application.
/// Implements IApplicationDbContext for the Application layer.
/// Extends IdentityDbContext for ASP.NET Core Identity support.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<IdentityUser>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Schema prefix for Identity tables to keep them organized
        builder.HasDefaultSchema("public");

        // Future: configure domain entity mappings per module here
        // builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
