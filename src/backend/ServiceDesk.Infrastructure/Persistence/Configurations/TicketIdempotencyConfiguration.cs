using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Ticketing;
using ServiceDesk.Infrastructure.Ticketing;
namespace ServiceDesk.Infrastructure.Persistence.Configurations;
internal sealed class TicketIdempotencyConfiguration : IEntityTypeConfiguration<TicketIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<TicketIdempotencyRecord> entity)
    {
            entity.ToTable("ticket_idempotency", table =>
                table.HasCheckConstraint("CK_ticket_idempotency_expiry", "expires_at > created_at"));
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.RequesterId).HasColumnName("requester_id");
            entity.Property(record => record.Method).HasColumnName("method").HasMaxLength(10).IsRequired();
            entity.Property(record => record.Resource).HasColumnName("resource").HasMaxLength(100).IsRequired();
            entity.Property(record => record.KeyHash).HasColumnName("key_hash").HasMaxLength(64).IsRequired();
            entity.Property(record => record.PayloadHash).HasColumnName("payload_hash").HasMaxLength(64).IsRequired();
            entity.Property(record => record.TicketId).HasColumnName("ticket_id");
            entity.Property(record => record.ResponseJson).HasColumnName("response_json").HasColumnType("jsonb").IsRequired();
            entity.Property(record => record.CreatedAt).HasColumnName("created_at");
            entity.Property(record => record.ExpiresAt).HasColumnName("expires_at");
            entity.HasIndex(record => new { record.RequesterId, record.Method, record.Resource, record.KeyHash })
                .IsUnique().HasDatabaseName("UX_ticket_idempotency_scope");
            entity.HasIndex(record => record.ExpiresAt);
            entity.HasOne<ServiceDesk.Domain.Ticketing.Ticket>().WithMany()
                .HasForeignKey(record => record.TicketId).OnDelete(DeleteBehavior.Restrict);

    }
}
