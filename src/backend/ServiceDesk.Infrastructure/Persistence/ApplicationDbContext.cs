using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Identity;
using ServiceDesk.Modules.Identity.Domain;
using ServiceDesk.Modules.Knowledge;
using ServiceDesk.Modules.Knowledge.Domain;
using ServiceDesk.Modules.Ticketing;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.Modules.Ticketing.Infrastructure;

namespace ServiceDesk.Infrastructure.Persistence;

/// <summary>
/// DbContext chính của ứng dụng:
/// - Kế thừa IdentityDbContext để quản lý bảng người dùng và vai trò của ASP.NET Core Identity.
/// - Quản lý các DbSet cho Tickets và TicketIdempotencyRecords.
/// - Quản lý các DbSet cho Knowledge Base Articles, Tags và Versions.
/// - Quản lý các DbSet cho Departments, RefreshTokens và OtpCodes.
/// - Nạp cấu hình EntityTypeConfiguration từ assembly của từng module qua OnModelCreating.
/// - Là context duy nhất sở hữu migration; adapter trong module dùng nó qua bí danh DbContext.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<TicketIdempotencyRecord> TicketIdempotencyRecords { get; set; }
    public DbSet<TicketComment> TicketComments { get; set; }
    public DbSet<TicketAttachment> TicketAttachments { get; set; }
    public DbSet<AttachmentUploadGrant> AttachmentUploadGrants { get; set; }
    public DbSet<SupportTeam> SupportTeams { get; set; }
    public DbSet<SupportTeamMember> SupportTeamMembers { get; set; }

    public DbSet<Article> Articles { get; set; }
    public DbSet<ArticleTag> ArticleTags { get; set; }
    public DbSet<ArticleVersion> ArticleVersions { get; set; }

    public DbSet<Department> Departments { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<OtpCode> OtpCodes { get; set; }

    /// <summary>Assembly của các module có bảng trong database dùng chung.</summary>
    private static readonly System.Reflection.Assembly[] ModuleModelAssemblies =
        [typeof(IdentityModule).Assembly, typeof(TicketingModule).Assembly, typeof(KnowledgeModule).Assembly];

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("public");
        // Mỗi module sở hữu cấu hình ánh xạ (IEntityTypeConfiguration) của bảng mình; context chỉ gom lại.
        foreach (var assembly in ModuleModelAssemblies)
            builder.ApplyConfigurationsFromAssembly(assembly);
    }
}
