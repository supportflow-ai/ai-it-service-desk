using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Ticketing;
using ServiceDesk.Infrastructure.Persistence;

namespace ServiceDesk.Infrastructure.Ticketing;

internal sealed class PostgresTicketAttachmentStore(ApplicationDbContext context) : ITicketAttachmentStore
{
    public async Task CreateUploadGrantAsync(AttachmentUploadGrant grant, CancellationToken cancellationToken = default)
    {
        context.AttachmentUploadGrants.Add(grant);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<TicketAttachmentDto> ConfirmUploadAsync(
        Guid ticketId,
        Guid actorId,
        ConfirmAttachmentUploadRequest request,
        ObjectStorageObjectInfo objectInfo,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var grant = await context.AttachmentUploadGrants.AsNoTracking().SingleOrDefaultAsync(g =>
            g.TicketId == ticketId && g.ActorId == actorId && g.StorageKey == request.StorageKey,
            cancellationToken);

        if (grant is null || grant.ConsumedAt != null || grant.ExpiresAt < now ||
            grant.FileName != request.FileName || grant.FileSizeBytes != request.FileSizeBytes ||
            !string.Equals(grant.ContentType, request.ContentType, StringComparison.OrdinalIgnoreCase) ||
            objectInfo.Size != grant.FileSizeBytes ||
            !string.Equals(objectInfo.ContentType, grant.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Xác nhận tải tệp không hợp lệ, đã hết hạn hoặc tệp không khớp quyền đã cấp.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var consumed = await context.AttachmentUploadGrants
            .Where(g => g.Id == grant.Id && g.ConsumedAt == null && g.ExpiresAt >= now)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.ConsumedAt, now), cancellationToken);
        if (consumed != 1)
        {
            throw new ArgumentException("Quyền tải tệp đã được sử dụng hoặc hết hạn.");
        }

        var attachment = TicketAttachment.Create(ticketId, grant.FileName, objectInfo.Size, objectInfo.ContentType, grant.StorageKey, actorId, now);
        context.TicketAttachments.Add(attachment);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new TicketAttachmentDto(attachment.Id, attachment.TicketId, attachment.FileName, attachment.FileSizeBytes,
            attachment.ContentType, attachment.StorageKey, attachment.UploadedBy, attachment.CreatedAt);
    }

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
