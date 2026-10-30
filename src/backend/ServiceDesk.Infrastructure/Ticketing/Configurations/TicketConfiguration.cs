using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Infrastructure.Ticketing.Configurations;

/// <summary>
/// Cấu hình ánh xạ bảng tickets trong PostgreSQL bằng EF Core:
/// - Khóa chính, chỉ mục tra cứu theo requester, status, ticket_number.
/// - Cột hệ thống xmin làm concurrency token phát hiện xung đột dữ liệu đồng thời.
/// </summary>
internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> entity)
    {
        entity.ToTable("tickets");
        entity.HasKey(t => t.Id);

        entity.HasIndex(t => t.TicketNumber).IsUnique();
        entity.HasIndex(t => t.RequesterId);
        entity.HasIndex(t => new { t.RequesterId, t.CreatedAt, t.Id })
            .HasDatabaseName("IX_tickets_requester_id_created_at_id");
        entity.HasIndex(t => t.Status);
        entity.HasIndex(t => t.AssignedToId);
        entity.HasIndex(t => t.Priority);

        entity.Property(t => t.RequesterId)
            .HasColumnName("requester_id")
            .IsRequired();

        entity.Property(t => t.RequesterDepartmentId)
            .HasColumnName("requester_department_id");

        entity.Property(t => t.AssignedToId)
            .HasColumnName("assigned_to_id");

        entity.Property(t => t.AssignedAt)
            .HasColumnName("assigned_at");

        entity.Property(t => t.Impact)
            .HasColumnName("impact");

        entity.Property(t => t.Urgency)
            .HasColumnName("urgency");

        entity.Property(t => t.Priority)
            .HasColumnName("priority");

        entity.Property(t => t.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        entity.Property(t => t.Description)
            .HasColumnName("description")
            .IsRequired();

        entity.Property(t => t.CategoryId)
            .HasColumnName("category_id")
            .HasMaxLength(20)
            .IsRequired();

        entity.Property(t => t.Status)
            .HasColumnName("status")
            .IsRequired();

        entity.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        entity.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        entity.Property(t => t.TicketNumber)
            .HasColumnName("ticket_number").HasMaxLength(30)
            .HasDefaultValueSql("public.next_ticket_number()")
            .ValueGeneratedOnAdd().IsRequired();

        entity.Property<uint>("xmin").HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
    }
}
