using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Infrastructure.Ticketing.Configurations;

internal sealed class TicketAttachmentConfiguration : IEntityTypeConfiguration<TicketAttachment>
{
    public void Configure(EntityTypeBuilder<TicketAttachment> entity)
    {
        entity.ToTable("ticket_attachments");
        entity.HasKey(a => a.Id);

        entity.Property(a => a.TicketId).HasColumnName("ticket_id").IsRequired();
        entity.Property(a => a.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
        entity.Property(a => a.FileSizeBytes).HasColumnName("file_size_bytes").IsRequired();
        entity.Property(a => a.ContentType).HasColumnName("content_type").HasMaxLength(100).IsRequired();
        entity.Property(a => a.StorageKey).HasColumnName("storage_key").HasMaxLength(500).IsRequired();
        entity.Property(a => a.UploadedBy).HasColumnName("uploaded_by").IsRequired();
        entity.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();

        entity.HasIndex(a => a.TicketId);
    }
}
