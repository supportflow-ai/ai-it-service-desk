using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
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

    public DbSet<Ticket> Tickets { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("public");

        builder.Entity<Ticket>(entity =>
        {
            entity.ToTable("tickets");
            entity.HasKey(t => t.Id);

            entity.HasIndex(t => t.TicketNumber).IsUnique();
            entity.HasIndex(t => t.RequesterId);
            entity.HasIndex(t => t.Status);

            entity.Property(t => t.RequesterId)
                .HasColumnName("requester_id")
                .IsRequired();

            entity.Property(t => t.RequesterDepartmentId)
                .HasColumnName("requester_department_id");

            entity.Property(t => t.Title)
                .HasColumnName("title")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(t => t.Description)
                .HasColumnName("description")
                .IsRequired();

            entity.Property(t => t.CategoryId)
                .HasColumnName("category_id")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(t => t.Status)
                .HasColumnName("status")
                .IsRequired();

            entity.Property(t => t.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            entity.Property(t => t.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired();

            if (Database.ProviderName != "Microsoft.EntityFrameworkCore.Sqlite")
            {
                // Sequence format behavior: LPAD does not truncate when sequence > 9999.
                // Numbers >= 10000 return full number (e.g. IT-YYYY-10000) which fits in VARCHAR(30).
                entity.Property(t => t.TicketNumber)
                    .HasColumnName("ticket_number")
                    .HasMaxLength(30)
                    .HasDefaultValueSql("'IT-' || TO_CHAR(NOW(), 'YYYY') || '-' || LPAD(nextval('ticket_number_seq')::TEXT, 4, '0')")
                    .ValueGeneratedOnAdd()
                    .IsRequired();

                entity.Property<uint>("xmin")
                    .HasColumnType("xid")
                    .ValueGeneratedOnAddOrUpdate()
                    .IsConcurrencyToken();
            }
            else
            {
                entity.Property(t => t.TicketNumber)
                    .HasColumnName("ticket_number")
                    .HasMaxLength(30)
                    .HasDefaultValueSql("''")
                    .ValueGeneratedOnAdd()
                    .IsRequired();
            }
        });
    }
}
