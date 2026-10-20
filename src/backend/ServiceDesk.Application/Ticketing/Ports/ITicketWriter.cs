using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Idempotency;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.Ticketing.Ports;

/// <summary>
/// Cổng thực thi các thao tác ghi dữ liệu ticket nguyên tử (Atomic persistence), bao gồm cả Idempotency receipt.
/// </summary>
public interface ITicketWriter
{
    /// <summary>Tạo mới ticket kèm dấu vân tay Idempotency nếu có.</summary>
    Task<TicketDto> CreateAsync(Ticket ticket, CreateTicketFingerprint? fingerprint, CancellationToken cancellationToken);

    /// <summary>Tải ticket lên từ cơ sở dữ liệu để chuẩn bị cập nhật trạng thái.</summary>
    Task<Ticket?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lưu các thay đổi của ticket vào cơ sở dữ liệu.</summary>
    Task<TicketDto> UpdateTicketAsync(Ticket ticket, CancellationToken cancellationToken);
}
