using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Modules.Identity.Domain;

namespace ServiceDesk.Modules.Identity.Infrastructure.Configurations;

public sealed class PasswordResetJobConfiguration : IEntityTypeConfiguration<PasswordResetJob>
{
    public void Configure(EntityTypeBuilder<PasswordResetJob> entity)
    {
        entity.ToTable("password_reset_jobs", "public");
        entity.HasKey(j => j.Id);
        entity.Property(j => j.Id).HasColumnName("id");
        entity.Property(j => j.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        entity.Property(j => j.CreatedAt).HasColumnName("created_at");
        entity.Property(j => j.ExpiresAt).HasColumnName("expires_at");
        entity.Property(j => j.NextAttemptAt).HasColumnName("next_attempt_at");
        entity.Property(j => j.Attempts).HasColumnName("attempts");
        entity.Property(j => j.LeaseId).HasColumnName("lease_id");
        entity.Property(j => j.LeaseUntil).HasColumnName("lease_until");
        entity.Property(j => j.OtpCodeId).HasColumnName("otp_code_id");
        entity.Property(j => j.ProtectedCode).HasColumnName("protected_code").HasMaxLength(1024);
        entity.Property(j => j.CompletedAt).HasColumnName("completed_at");
        entity.Property(j => j.FailureCode).HasColumnName("failure_code").HasMaxLength(64);
        entity.HasIndex(j => new { j.NextAttemptAt, j.CreatedAt }).HasFilter("completed_at IS NULL");
        entity.HasIndex(j => j.CompletedAt);
    }
}
