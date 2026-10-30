using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Ticketing;
using ServiceDesk.Infrastructure.Persistence;

namespace ServiceDesk.Infrastructure.Ticketing;

internal sealed class PostgresTicketAttachmentStore(ApplicationDbContext context) : ITicketAttachmentStore
{
    public async Task<TicketAttachmentDto> AddAsync(TicketAttachment attachment, CancellationToken cancellationToken = default)
    {
        context.TicketAttachments.Add(attachment);
        await context.SaveChangesAsync(cancellationToken);
        return new TicketAttachmentDto(
            attachment.Id,
            attachment.TicketId,
            attachment.FileName,
            attachment.FileSizeBytes,
            attachment.ContentType,
            attachment.StorageKey,
            attachment.UploadedBy,
            attachment.CreatedAt);
    }

    public async Task<IReadOnlyList<TicketAttachmentDto>> GetForTicketAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        return await context.TicketAttachments.AsNoTracking()
            .Where(a => a.TicketId == ticketId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new TicketAttachmentDto(
                a.Id,
                a.TicketId,
                a.FileName,
                a.FileSizeBytes,
                a.ContentType,
                a.StorageKey,
                a.UploadedBy,
                a.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid ticketId, string storageKey, CancellationToken cancellationToken = default)
    {
        return context.TicketAttachments.AsNoTracking()
            .AnyAsync(a => a.TicketId == ticketId && a.StorageKey == storageKey, cancellationToken);
    }
}
