using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Modules.Knowledge.Domain;

namespace ServiceDesk.Modules.Knowledge.Infrastructure.Configurations;

/// <summary>
/// Cấu hình ánh xạ bảng articles trong PostgreSQL bằng EF Core:
/// - Khóa chính Id, các thuộc tính bắt buộc và tùy chọn.
/// - Cột hệ thống xmin làm concurrency token phát hiện xung đột dữ liệu đồng thời.
/// - Ánh xạ quan hệ Tags và Versions tới backing field tương ứng.
/// - Chỉ mục tìm kiếm theo Status, AuthorId, CreatedAt, Title.
/// </summary>
internal sealed class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> entity)
    {
        entity.ToTable("articles");
        entity.HasKey(a => a.Id);

        entity.Property(a => a.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        entity.Property(a => a.Content)
            .HasColumnName("content")
            .IsRequired();

        entity.Property(a => a.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        entity.Property(a => a.AuthorId)
            .HasColumnName("author_id")
            .IsRequired();

        entity.Property(a => a.Version)
            .HasColumnName("version")
            .IsRequired();

        entity.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        entity.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        entity.Property(a => a.PublishedAt)
            .HasColumnName("published_at");

        entity.Property(a => a.ArchivedAt)
            .HasColumnName("archived_at");

        entity.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        entity.HasMany(a => a.Tags)
            .WithOne()
            .HasForeignKey(t => t.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.Navigation(a => a.Tags)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        entity.HasMany(a => a.Versions)
            .WithOne()
            .HasForeignKey(v => v.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.Navigation(a => a.Versions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        entity.HasIndex(a => a.Status);
        entity.HasIndex(a => a.AuthorId);
        entity.HasIndex(a => a.CreatedAt);
        entity.HasIndex(a => a.Title);
    }
}
