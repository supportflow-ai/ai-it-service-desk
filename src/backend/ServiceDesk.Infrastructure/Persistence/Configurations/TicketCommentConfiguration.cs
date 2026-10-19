using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Infrastructure.Persistence.Configurations;

public sealed class TicketCommentConfiguration : IEntityTypeConfiguration<TicketComment>
{
    public void Configure(EntityTypeBuilder<TicketComment> builder)
    {
        builder.ToTable("TicketComments");
        builder.HasKey(comment => comment.Id);
        builder.Property(comment => comment.AuthorId).HasMaxLength(450).IsRequired();
        builder.Property(comment => comment.Content).HasMaxLength(4000).IsRequired();
        builder.HasIndex(comment => new { comment.TicketId, comment.CreatedAt });
    }
}
