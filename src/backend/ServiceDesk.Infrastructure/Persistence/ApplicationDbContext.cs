using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Domain.Ticketing;
using ServiceDesk.Infrastructure.Ticketing;

namespace ServiceDesk.Infrastructure.Persistence;

/// <summary>
/// DbContext chính của ứng dụng:
/// - Kế thừa IdentityDbContext để quản lý bảng người dùng và vai trò của ASP.NET Core Identity.
/// - Quản lý các DbSet cho Tickets và TicketIdempotencyRecords.
/// - Tự động nạp cấu hình EntityTypeConfiguration từ assembly qua OnModelCreating.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<TicketIdempotencyRecord> TicketIdempotencyRecords { get; set; }
    public DbSet<TicketComment> TicketComments { get; set; }
    public DbSet<TicketAttachment> TicketAttachments { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("public");
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
