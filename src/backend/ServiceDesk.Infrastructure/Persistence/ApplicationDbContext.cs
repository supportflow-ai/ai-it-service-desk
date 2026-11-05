using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Domain.Knowledge;
using ServiceDesk.Domain.Ticketing;
using ServiceDesk.Infrastructure.Ticketing;
using ServiceDesk.Infrastructure.Knowledge.Indexing;

namespace ServiceDesk.Infrastructure.Persistence;

/// <summary>
/// DbContext chính của ứng dụng:
/// - Kế thừa IdentityDbContext để quản lý bảng người dùng và vai trò của ASP.NET Core Identity.
/// - Quản lý các DbSet cho Tickets và TicketIdempotencyRecords.
/// - Quản lý các DbSet cho Knowledge Base Articles, Tags và Versions.
/// - Tự động nạp cấu hình EntityTypeConfiguration từ assembly qua OnModelCreating.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<TicketIdempotencyRecord> TicketIdempotencyRecords { get; set; }
    public DbSet<TicketComment> TicketComments { get; set; }
    public DbSet<TicketAttachment> TicketAttachments { get; set; }
    public DbSet<AttachmentUploadGrant> AttachmentUploadGrants { get; set; }

    public DbSet<Article> Articles { get; set; }
    public DbSet<ArticleTag> ArticleTags { get; set; }
    public DbSet<ArticleVersion> ArticleVersions { get; set; }
    internal DbSet<KnowledgeIndexJob> KnowledgeIndexJobs => Set<KnowledgeIndexJob>();
    internal DbSet<KnowledgeIndexState> KnowledgeIndexStates => Set<KnowledgeIndexState>();
    internal DbSet<KnowledgeIndexSet> KnowledgeIndexSets => Set<KnowledgeIndexSet>();
    internal DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("public");
        builder.HasPostgresExtension("vector");
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
