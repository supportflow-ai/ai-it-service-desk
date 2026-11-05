using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Knowledge;
using ServiceDesk.Infrastructure.Knowledge.Indexing;

namespace ServiceDesk.Infrastructure.Knowledge.Configurations;

internal sealed class KnowledgeIndexJobConfiguration : IEntityTypeConfiguration<KnowledgeIndexJob>
{
    public void Configure(EntityTypeBuilder<KnowledgeIndexJob> e)
    {
        e.ToTable("knowledge_index_jobs", t => t.HasCheckConstraint("CK_index_job_status",
            "\"Status\" IN ('Pending','Running','Succeeded','Failed','Superseded')"));
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.SourceHash).HasMaxLength(64).IsRequired();
        e.Property(x => x.ProfileId).HasMaxLength(100).IsRequired();
        e.Property(x => x.ProfileHash).HasMaxLength(64).IsRequired();
        e.Property(x => x.Status).HasMaxLength(20).IsRequired();
        e.Property(x => x.ErrorCode).HasMaxLength(100);
        e.HasOne<ArticleVersion>().WithMany().HasForeignKey(x => x.ArticleVersionId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Article>().WithMany().HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.Status, x.NextAttemptAt, x.CreatedAt });
        e.HasIndex(x => new { x.ArticleVersionId, x.ProfileHash });
    }
}

internal sealed class KnowledgeIndexStateConfiguration : IEntityTypeConfiguration<KnowledgeIndexState>
{
    public void Configure(EntityTypeBuilder<KnowledgeIndexState> e)
    {
        e.ToTable("knowledge_index_states");
        e.HasKey(x => x.ArticleId);
        e.Property(x => x.ArticleId).ValueGeneratedNever();
        e.HasOne<Article>().WithOne().HasForeignKey<KnowledgeIndexState>(x => x.ArticleId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<KnowledgeIndexJob>().WithMany().HasForeignKey(x => x.RequestedJobId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<KnowledgeIndexSet>().WithMany().HasForeignKey(x => x.ActiveIndexSetId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class KnowledgeIndexSetConfiguration : IEntityTypeConfiguration<KnowledgeIndexSet>
{
    public void Configure(EntityTypeBuilder<KnowledgeIndexSet> e)
    {
        e.ToTable("knowledge_index_sets", t => t.HasCheckConstraint("CK_index_set_dimension", "\"Dimension\" = 384"));
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.SourceHash).HasMaxLength(64).IsRequired();
        e.Property(x => x.NormalizedContentHash).HasMaxLength(64).IsRequired();
        e.Property(x => x.ProfileId).HasMaxLength(100).IsRequired();
        e.Property(x => x.ProfileHash).HasMaxLength(64).IsRequired();
        e.Property(x => x.ProfileJson).HasColumnType("jsonb").IsRequired();
        e.HasOne<KnowledgeIndexJob>().WithOne().HasForeignKey<KnowledgeIndexSet>(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ArticleVersion>().WithMany().HasForeignKey(x => x.ArticleVersionId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.ArticleId, x.VersionNumber, x.ProfileHash });
    }
}

internal sealed class KnowledgeChunkConfiguration : IEntityTypeConfiguration<KnowledgeChunk>
{
    public void Configure(EntityTypeBuilder<KnowledgeChunk> e)
    {
        e.ToTable("knowledge_chunks", t => t.HasCheckConstraint("CK_chunk_offsets",
            "\"StartChar\" >= 0 AND \"EndChar\" > \"StartChar\" AND \"Index\" >= 0"));
        e.HasKey(x => new { x.IndexSetId, x.Index });
        e.Property(x => x.ChunkId).HasMaxLength(64).IsRequired();
        e.Property(x => x.TextHash).HasMaxLength(64).IsRequired();
        e.Property(x => x.Text).IsRequired();
        e.Property(x => x.HeadingPath).HasColumnType("text[]").IsRequired();
        e.HasOne<KnowledgeIndexSet>().WithMany().HasForeignKey(x => x.IndexSetId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.IndexSetId, x.ChunkId }).IsUnique();
    }
}
