using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.Ticketing.Ports;

/// <summary>
/// Cổng lưu trữ và truy vấn metadata tệp đính kèm thuộc ticket.
/// </summary>
public interface ITicketAttachmentStore
{
    Task CreateUploadGrantAsync(AttachmentUploadGrant upload, CancellationToken cancellationToken = default);
    Task<AttachmentUploadGrant?> GetUploadGrantAsync(Guid ticketId, string storageKey, Guid actor, CancellationToken cancellationToken = default);
    Task<TicketAttachmentDto?> GetByIdAsync(Guid attachmentId, CancellationToken cancellationToken = default);
    Task<TicketAttachmentDto> ConfirmAsync(Guid uploadId, TicketAttachment attachment, DateTime now, CancellationToken cancellationToken = default);
    Task<TicketAttachmentDto> AddAsync(TicketAttachment attachment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TicketAttachmentDto>> GetForTicketAsync(Guid ticketId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid ticketId, string storageKey, CancellationToken cancellationToken = default);
}
