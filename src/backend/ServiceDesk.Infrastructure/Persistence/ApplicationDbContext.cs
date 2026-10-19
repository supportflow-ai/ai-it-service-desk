using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Domain.Ticketing;

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

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("public");
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<TicketComment>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.State = EntityState.Added;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
