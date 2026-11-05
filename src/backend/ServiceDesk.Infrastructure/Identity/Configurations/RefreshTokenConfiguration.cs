using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Infrastructure.Identity.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> entity)
    {
        entity.ToTable("refresh_tokens");
        entity.HasKey(r => r.Id);

        entity.Property(r => r.Id).HasColumnName("id");
        entity.Property(r => r.UserId).HasColumnName("user_id").HasMaxLength(450).IsRequired();
        entity.Property(r => r.TokenHash).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
        entity.Property(r => r.ExpiresAt).HasColumnName("expires_at").IsRequired();
        entity.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        entity.Property(r => r.RevokedAt).HasColumnName("revoked_at");
        entity.Property(r => r.DeviceInfo).HasColumnName("device_info").HasMaxLength(256);
        entity.Property(r => r.IpAddress).HasColumnName("ip_address").HasMaxLength(64);

        entity.HasIndex(r => r.UserId);
        entity.HasIndex(r => r.TokenHash);
    }
}
