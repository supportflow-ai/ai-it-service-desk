using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Ticketing;
using ServiceDesk.Infrastructure.Persistence;

namespace ServiceDesk.Infrastructure.Ticketing;

internal sealed class PostgresTicketAttachmentStore(ApplicationDbContext context) : ITicketAttachmentStore
{
    public async Task CreateUploadGrantAsync(AttachmentUploadGrant upload, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", cancellationToken);
        var scope = "attachment-upload:" + upload.ActorId;
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({scope}, 0))", cancellationToken);
        var now = await context.Database.SqlQuery<DateTime>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(cancellationToken);
        if (await context.AttachmentUploadGrants.CountAsync(value => value.ActorId == upload.ActorId && value.ConsumedAt == null && value.ExpiresAt > now, cancellationToken) >= 20)
            throw new ArgumentException("Đang có quá nhiều tệp chờ xác nhận. Vui lòng xác nhận hoặc chờ yêu cầu cũ hết hạn.");
        context.AttachmentUploadGrants.Add(upload);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task<AttachmentUploadGrant?> GetUploadGrantAsync(Guid ticketId, string storageKey, Guid actor, CancellationToken cancellationToken = default) =>
        context.AttachmentUploadGrants.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TicketId == ticketId && value.StorageKey == storageKey && value.ActorId == actor, cancellationToken);

    public async Task<TicketAttachmentDto?> GetByIdAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await context.TicketAttachments.AsNoTracking().SingleOrDefaultAsync(value => value.Id == attachmentId, cancellationToken);
        return attachment is null ? null : ToDto(attachment);
    }

    public async Task<TicketAttachmentDto> ConfirmAsync(Guid uploadId, TicketAttachment attachment, DateTime now, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({uploadId.ToString()}, 0))", cancellationToken);
        var upload = await context.AttachmentUploadGrants.SingleOrDefaultAsync(value => value.Id == uploadId, cancellationToken)
            ?? throw new KeyNotFoundException("Upload grant not found.");
        if (upload.AttachmentId is { } existingId)
        {
            var existing = await GetByIdAsync(existingId, cancellationToken)
                ?? throw new InvalidOperationException("Metadata xác nhận không còn tồn tại.");
            await transaction.CommitAsync(cancellationToken);
            return existing;
        }
        var databaseNow = await context.Database.SqlQuery<DateTime>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(cancellationToken);
        upload.Confirm(attachment, now > databaseNow ? now : databaseNow);
        context.TicketAttachments.Add(attachment);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(attachment);
    }

    private static TicketAttachmentDto ToDto(TicketAttachment value) => new(value.Id, value.TicketId,
        value.FileName, value.FileSizeBytes, value.ContentType, value.StorageKey, value.UploadedBy, value.CreatedAt);

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
