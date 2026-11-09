using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Modules.Identity.Domain;

namespace ServiceDesk.Modules.Identity.Infrastructure.Configurations;

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> entity)
    {
        entity.ToTable("departments");
        entity.HasKey(d => d.Id);

        entity.Property(d => d.Id).HasColumnName("id");
        entity.Property(d => d.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        entity.Property(d => d.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        entity.Property(d => d.Description).HasColumnName("description").HasMaxLength(500);
        entity.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();

        entity.HasIndex(d => d.Code).IsUnique();
    }
}
