using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Knowledge;

namespace ServiceDesk.Infrastructure.Knowledge.Configurations;

/// <summary>
/// Cấu hình ánh xạ bảng article_versions trong PostgreSQL bằng EF Core:
/// - Khóa chính Id, các trường metadata và nội dung phiên bản xuất bản.
/// - Chỉ mục tìm kiếm theo (ArticleId, VersionNumber).
/// </summary>
internal sealed class ArticleVersionConfiguration : IEntityTypeConfiguration<ArticleVersion>
{
    public void Configure(EntityTypeBuilder<ArticleVersion> entity)
    {
        entity.ToTable("article_versions");
        entity.HasKey(v => v.Id);

        entity.Property(v => v.ArticleId)
            .HasColumnName("article_id")
            .IsRequired();

        entity.Property(v => v.VersionNumber)
            .HasColumnName("version_number")
            .IsRequired();

        entity.Property(v => v.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        entity.Property(v => v.Content)
            .HasColumnName("content")
            .IsRequired();

        entity.Property(v => v.PublishedById)
            .HasColumnName("published_by_id")
            .IsRequired();

        entity.Property(v => v.PublishedAt)
            .HasColumnName("published_at")
            .IsRequired();

        entity.HasIndex(v => new { v.ArticleId, v.VersionNumber });
    }
}
