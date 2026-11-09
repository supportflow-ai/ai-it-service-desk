using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Modules.Knowledge.Domain;

namespace ServiceDesk.Modules.Knowledge.Infrastructure.Configurations;

/// <summary>
/// Cấu hình ánh xạ bảng article_tags trong PostgreSQL bằng EF Core:
/// - Khóa chính phức hợp (ArticleId, Name).
/// - Giới hạn độ dài và bắt buộc cho Name.
/// </summary>
internal sealed class ArticleTagConfiguration : IEntityTypeConfiguration<ArticleTag>
{
    public void Configure(EntityTypeBuilder<ArticleTag> entity)
    {
        entity.ToTable("article_tags");
        entity.HasKey(t => new { t.ArticleId, t.Name });

        entity.Property(t => t.ArticleId)
            .HasColumnName("article_id")
            .IsRequired();

        entity.Property(t => t.Name)
            .HasColumnName("name")
            .HasMaxLength(50)
            .IsRequired();
    }
}
