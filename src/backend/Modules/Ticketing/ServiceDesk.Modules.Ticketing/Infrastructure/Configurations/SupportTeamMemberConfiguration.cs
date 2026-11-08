using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Infrastructure.Configurations;

/// <summary>
/// Bảng support_team_members với khóa chính (team_id, user_id). Có khóa ngoại tới nhóm; cố ý không có khóa ngoại tới
/// bảng người dùng Identity để Ticketing không phụ thuộc bảng của module khác.
/// </summary>
internal sealed class SupportTeamMemberConfiguration : IEntityTypeConfiguration<SupportTeamMember>
{
    public void Configure(EntityTypeBuilder<SupportTeamMember> entity)
    {
        entity.ToTable("support_team_members");
        entity.HasKey(m => new { m.TeamId, m.UserId });

        entity.Property(m => m.TeamId).HasColumnName("team_id");
        entity.Property(m => m.UserId).HasColumnName("user_id");
        entity.Property(m => m.IsLead).HasColumnName("is_lead").IsRequired();

        entity.HasIndex(m => m.UserId);

        entity.HasOne<SupportTeam>().WithMany()
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
