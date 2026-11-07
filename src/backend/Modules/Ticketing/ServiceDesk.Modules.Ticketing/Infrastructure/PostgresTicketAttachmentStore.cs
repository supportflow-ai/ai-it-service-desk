using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Infrastructure;

internal sealed class PostgresTicketAttachmentStore(DbContext context) : ITicketAttachmentStore
{
    internal const string ClosedTicketMessage = "Không thể xác nhận tệp cho vé đã kết thúc.";

    public async Task CreateUploadGrantAsync(AttachmentUploadGrant upload, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", cancellationToken);
        var scope = "attachment-upload:" + upload.ActorId;
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({scope}, 0))", cancellationToken);
        var now = await context.Database.SqlQuery<DateTime>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(cancellationToken);
        if (await context.Set<AttachmentUploadGrant>().CountAsync(value => value.ActorId == upload.ActorId && value.ConsumedAt == null && value.ExpiresAt > now, cancellationToken) >= 20)
            throw new ArgumentException("Đang có quá nhiều tệp chờ xác nhận. Vui lòng xác nhận hoặc chờ yêu cầu cũ hết hạn.");
        context.Set<AttachmentUploadGrant>().Add(upload);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task<AttachmentUploadGrant?> GetUploadGrantAsync(Guid ticketId, string storageKey, Guid actor, CancellationToken cancellationToken = default) =>
        context.Set<AttachmentUploadGrant>().AsNoTracking().SingleOrDefaultAsync(value =>
            value.TicketId == ticketId && value.StorageKey == storageKey && value.ActorId == actor, cancellationToken);

    public async Task<TicketAttachmentDto?> GetByIdAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await context.Set<TicketAttachment>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == attachmentId, cancellationToken);
        return attachment is null ? null : ToDto(attachment);
    }

    public async Task<TicketAttachmentDto> ConfirmAsync(Guid uploadId, TicketAttachment attachment, DateTime now, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({uploadId.ToString()}, 0))", cancellationToken);
        var upload = await context.Set<AttachmentUploadGrant>().SingleOrDefaultAsync(value => value.Id == uploadId, cancellationToken)
            ?? throw new KeyNotFoundException("Upload grant not found.");
        if (upload.AttachmentId is { } existingId)
        {
            var existing = await GetByIdAsync(existingId, cancellationToken)
                ?? throw new InvalidOperationException("Metadata xác nhận không còn tồn tại.");
            await transaction.CommitAsync(cancellationToken);
            return existing;
        }
        // Kiểm lại trạng thái ticket trong giao dịch với khóa chia sẻ: lệnh đóng/hủy ticket (UPDATE dòng ticket) phải chờ
        // giao dịch này, hoặc đã commit trước và bị phát hiện ở đây. Không ghi tệp vào ticket đã kết thúc.
        await TicketRowLock.RequireOpenAsync(context, attachment.TicketId, ClosedTicketMessage, cancellationToken);
        var databaseNow = await context.Database.SqlQuery<DateTime>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(cancellationToken);
        upload.Confirm(attachment, now > databaseNow ? now : databaseNow);
        context.Set<TicketAttachment>().Add(attachment);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(attachment);
    }

    private static TicketAttachmentDto ToDto(TicketAttachment value) => new(value.Id, value.TicketId,
        value.FileName, value.FileSizeBytes, value.ContentType, value.StorageKey, value.UploadedBy, value.CreatedAt);

    public async Task<TicketAttachmentDto> AddAsync(TicketAttachment attachment, CancellationToken cancellationToken = default)
    {
        context.Set<TicketAttachment>().Add(attachment);
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
        return await context.Set<TicketAttachment>().AsNoTracking()
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
        return context.Set<TicketAttachment>().AsNoTracking()
            .AnyAsync(a => a.TicketId == ticketId && a.StorageKey == storageKey, cancellationToken);
    }
}
