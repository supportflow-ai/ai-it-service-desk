using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Infrastructure.Ticketing.Configurations;

internal sealed class TicketCommentConfiguration : IEntityTypeConfiguration<TicketComment>
{
    public void Configure(EntityTypeBuilder<TicketComment> entity)
    {
        entity.ToTable("ticket_comments");
        entity.HasKey(c => c.Id);

        entity.Property(c => c.TicketId).HasColumnName("ticket_id").IsRequired();
        entity.Property(c => c.AuthorId).HasColumnName("author_id").IsRequired();
        entity.Property(c => c.AuthorName).HasColumnName("author_name").HasMaxLength(200).IsRequired();
        entity.Property(c => c.AuthorRole).HasColumnName("author_role").HasMaxLength(50).IsRequired();
        entity.Property(c => c.Content).HasColumnName("content").HasMaxLength(4000).IsRequired();
        entity.Property(c => c.IsInternal).HasColumnName("is_internal").IsRequired();
        entity.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();

        entity.HasIndex(c => new { c.TicketId, c.CreatedAt });
    }
}
