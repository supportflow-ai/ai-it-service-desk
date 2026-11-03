using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Infrastructure.Ticketing.Configurations;

internal sealed class AttachmentUploadGrantConfiguration : IEntityTypeConfiguration<AttachmentUploadGrant>
{
    public void Configure(EntityTypeBuilder<AttachmentUploadGrant> entity)
    {
        entity.ToTable("attachment_upload_grants");
        entity.HasKey(g => g.Id);

        entity.Property(g => g.TicketId).HasColumnName("ticket_id").IsRequired();
        entity.Property(g => g.ActorId).HasColumnName("actor_id").IsRequired();
        entity.Property(g => g.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
        entity.Property(g => g.FileSizeBytes).HasColumnName("file_size_bytes").IsRequired();
        entity.Property(g => g.ContentType).HasColumnName("content_type").HasMaxLength(100).IsRequired();
        entity.Property(g => g.StorageKey).HasColumnName("storage_key").HasMaxLength(500).IsRequired();
        entity.Property(g => g.ExpiresAt).HasColumnName("expires_at").IsRequired();
        entity.Property(g => g.ConsumedAt).HasColumnName("consumed_at");
        entity.Property(g => g.AttachmentId).HasColumnName("attachment_id");

        entity.HasIndex(g => g.StorageKey).IsUnique();
        entity.HasIndex(g => new { g.ActorId, g.ExpiresAt });
        entity.HasIndex(g => g.AttachmentId).IsUnique();
        entity.HasOne<TicketAttachment>().WithMany().HasForeignKey(g => g.AttachmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
