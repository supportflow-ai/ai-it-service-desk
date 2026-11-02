using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.Ticketing.Ports;

/// <summary>
/// Cổng lưu trữ và truy vấn metadata tệp đính kèm thuộc ticket.
/// </summary>
public interface ITicketAttachmentStore
{
    Task CreateUploadGrantAsync(AttachmentUploadGrant grant, CancellationToken cancellationToken = default);
    Task<TicketAttachmentDto> ConfirmUploadAsync(Guid ticketId, Guid actorId, ConfirmAttachmentUploadRequest request, ObjectStorageObjectInfo objectInfo, DateTime now, CancellationToken cancellationToken = default);
    Task<TicketAttachmentDto> AddAsync(TicketAttachment attachment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TicketAttachmentDto>> GetForTicketAsync(Guid ticketId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid ticketId, string storageKey, CancellationToken cancellationToken = default);
}
