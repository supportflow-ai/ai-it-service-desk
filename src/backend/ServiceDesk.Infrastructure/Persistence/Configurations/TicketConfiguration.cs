using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Infrastructure.Persistence.Configurations;

public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");
        builder.HasKey(ticket => ticket.Id);
        builder.Property(ticket => ticket.RequesterId).HasMaxLength(450).IsRequired();
        builder.Property(ticket => ticket.AssigneeId).HasMaxLength(450);
        builder.Property(ticket => ticket.Title).HasMaxLength(200).IsRequired();
        builder.Property(ticket => ticket.Description).HasMaxLength(8000).IsRequired();
        builder.Property(ticket => ticket.Category).HasMaxLength(100).IsRequired();
        builder.Property(ticket => ticket.Impact).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(ticket => ticket.Urgency).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(ticket => ticket.Priority).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(ticket => ticket.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(ticket => ticket.Version).IsConcurrencyToken();

        builder.HasIndex(ticket => new { ticket.RequesterId, ticket.CreatedAt });
        builder.HasIndex(ticket => new { ticket.AssigneeId, ticket.Status });
        builder.HasIndex(ticket => new { ticket.Status, ticket.CreatedAt });

        builder.HasMany(ticket => ticket.Comments)
            .WithOne()
            .HasForeignKey(comment => comment.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
