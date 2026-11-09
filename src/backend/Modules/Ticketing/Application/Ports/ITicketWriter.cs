using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Idempotency;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.Ports;

/// <summary>
/// Cổng thực thi các thao tác ghi dữ liệu ticket nguyên tử (Atomic persistence), bao gồm cả Idempotency receipt.
/// </summary>
public interface ITicketWriter
{
    /// <summary>Tạo mới ticket kèm dấu vân tay Idempotency nếu có.</summary>
    Task<TicketDto> CreateAsync(Ticket ticket, CreateTicketFingerprint? fingerprint, CancellationToken cancellationToken);

    /// <summary>Tải ticket trong phạm vi đọc để chuẩn bị cập nhật trạng thái.</summary>
    Task<Ticket?> GetForUpdateAsync(Guid id, TicketReadScope scope, CancellationToken cancellationToken);

    /// <summary>Lưu các thay đổi của ticket vào cơ sở dữ liệu.</summary>
    Task<TicketDto> UpdateTicketAsync(Ticket ticket, CancellationToken cancellationToken);

    /// <summary>Lưu ticket và audit trong cùng một lần lưu nguyên tử.</summary>
    Task<TicketDto> UpdateTicketWithAuditAsync(Ticket ticket, TicketComment audit, CancellationToken cancellationToken);

    /// <summary>Lấy số phiên bản đồng thời hiện tại (concurrency revision) của ticket.</summary>
    uint GetRevision(Ticket ticket);
}
