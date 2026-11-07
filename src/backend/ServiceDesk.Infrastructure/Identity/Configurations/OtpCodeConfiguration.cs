using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Infrastructure.Identity.Configurations;

internal sealed class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> entity)
    {
        entity.ToTable("otp_codes");
        entity.HasKey(o => o.Id);

        entity.Property(o => o.Id).HasColumnName("id");
        entity.Property(o => o.Target).HasColumnName("target").HasMaxLength(256).IsRequired();
        entity.Property(o => o.CodeHash).HasColumnName("code_hash").HasMaxLength(128).IsRequired();
        entity.Property(o => o.Purpose).HasColumnName("purpose").HasMaxLength(50).IsRequired();
        entity.Property(o => o.ExpiresAt).HasColumnName("expires_at").IsRequired();
        entity.Property(o => o.CreatedAt).HasColumnName("created_at").IsRequired();
        entity.Property(o => o.IsUsed).HasColumnName("is_used").IsRequired();
        entity.Property(o => o.FailedAttempts).HasColumnName("failed_attempts").IsRequired();

        entity.HasIndex(o => new { o.Target, o.Purpose, o.IsUsed });
    }
}
