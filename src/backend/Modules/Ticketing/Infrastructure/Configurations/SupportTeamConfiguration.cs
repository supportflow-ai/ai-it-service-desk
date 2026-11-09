using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Infrastructure.Configurations;

/// <summary>
/// Bảng support_teams. Mã nhóm duy nhất; chỉ mục duy nhất có điều kiện bảo đảm tối đa một hàng đợi phân loại
/// thủ công (<c>is_triage_queue = TRUE</c>) ngay ở cơ sở dữ liệu.
/// </summary>
internal sealed class SupportTeamConfiguration : IEntityTypeConfiguration<SupportTeam>
{
    public void Configure(EntityTypeBuilder<SupportTeam> entity)
    {
        entity.ToTable("support_teams");
        entity.HasKey(t => t.Id);

        entity.Property(t => t.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        entity.Property(t => t.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        entity.Property(t => t.IsTriageQueue).HasColumnName("is_triage_queue").IsRequired();
        entity.Property(t => t.IsActive).HasColumnName("is_active").IsRequired();

        entity.HasIndex(t => t.Code).IsUnique();
        entity.HasIndex(t => t.IsTriageQueue).IsUnique()
            .HasDatabaseName("UX_support_teams_single_triage_queue")
            .HasFilter("is_triage_queue = TRUE");
    }
}
