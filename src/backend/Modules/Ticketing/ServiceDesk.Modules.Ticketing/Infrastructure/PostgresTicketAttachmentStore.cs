using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ServiceDesk.Modules.Ticketing.Application.ConfirmAttachmentUpload;
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
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
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
            await RequireCapacityAsync(attachment, cancellationToken);
            var databaseNow = await context.Database.SqlQuery<DateTime>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(cancellationToken);
            upload.Confirm(attachment, now > databaseNow ? now : databaseNow);
            context.Set<TicketAttachment>().Add(attachment);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToDto(attachment);
        }
        catch (Exception error) when (FindLockTimeout(error) is not null)
        {
            // Giao dịch đã rollback khi thoát khối using; upload grant vẫn chưa bị tiêu thụ nên client thử lại được.
            throw new AttachmentConfirmationBusyException(FindLockTimeout(error)!);
        }
    }

    /// <summary>
    /// Npgsql EF bọc lỗi tạm thời (kể cả 55P03) trong <see cref="InvalidOperationException"/>, nên phải tìm
    /// <see cref="PostgresException"/> trong chuỗi ngoại lệ thay vì bắt trực tiếp.
    /// </summary>
    private static PostgresException? FindLockTimeout(Exception? error)
    {
        while (error is not null)
        {
            if (error is PostgresException { SqlState: PostgresErrorCodes.LockNotAvailable } postgres) return postgres;
            error = error.InnerException;
        }
        return null;
    }

    private static TicketAttachmentDto ToDto(TicketAttachment value) => new(value.Id, value.TicketId,
        value.FileName, value.FileSizeBytes, value.ContentType, value.StorageKey, value.UploadedBy, value.CreatedAt);

    public async Task<TicketAttachmentDto> AddAsync(TicketAttachment attachment, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", cancellationToken);
        await RequireCapacityAsync(attachment, cancellationToken);
        context.Set<TicketAttachment>().Add(attachment);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(attachment);
    }

    private async Task RequireCapacityAsync(TicketAttachment attachment, CancellationToken cancellationToken)
    {
        await TicketRowLock.RequireOpenAsync(context, attachment.TicketId, ClosedTicketMessage, cancellationToken, serializeWrites: true);
        // Read Committed nhìn thấy lần xác nhận vừa commit sau khi giao dịch này lấy được khóa phiếu.
        var sizes = await context.Set<TicketAttachment>().AsNoTracking()
            .Where(file => file.TicketId == attachment.TicketId)
            .Select(file => file.FileSizeBytes)
            .Take(TicketAttachment.MaxFilesPerTicket)
            .ToListAsync(cancellationToken);
        attachment.RequireTicketCapacity(sizes);
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
