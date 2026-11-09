using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.ConfirmAttachmentUpload;

public record ConfirmAttachmentUploadCommand(Guid TicketId, ConfirmAttachmentUploadRequest Request);

public interface IConfirmAttachmentUpload
{
    Task<TicketAttachmentDto> ExecuteAsync(ConfirmAttachmentUploadCommand command, CancellationToken cancellationToken = default);
}

internal sealed class AttachmentConfirmationBusyException(Exception innerException)
    : RetryLaterException("Phiếu đang được cập nhật. Vui lòng thử xác nhận tệp lại.", innerException);

internal sealed class ConfirmAttachmentUploadUseCase(
    ITicketQueries ticketQueries,
    ITicketAttachmentStore attachmentStore,
    IObjectStorageService storageService,
    ICurrentUser currentUser,
    IClock clock) : IConfirmAttachmentUpload
{
    public async Task<TicketAttachmentDto> ExecuteAsync(ConfirmAttachmentUploadCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(command);
        var req = command.Request;

        var pendingPrefix = $"uploads/pending/{command.TicketId}/";
        var legacyPrefix = $"tickets/{command.TicketId}/";
        if (string.IsNullOrWhiteSpace(req.StorageKey) ||
            (!req.StorageKey.StartsWith(pendingPrefix, StringComparison.Ordinal) &&
             !req.StorageKey.StartsWith(legacyPrefix, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Storage key không hợp lệ hoặc không thuộc về vé này.");
        }

        var scope = TicketPermissions.ReadScopeFor(currentUser, actorId, includeOwner: true);

        var ticket = await ticketQueries.GetByIdAsync(command.TicketId, scope, cancellationToken);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found.");
        if (ticket.Status is (int)TicketStatus.Closed or (int)TicketStatus.Cancelled)
            throw new ArgumentException("Không thể xác nhận tệp cho vé đã kết thúc.");

        var upload = await attachmentStore.GetUploadGrantAsync(command.TicketId, req.StorageKey, actorId, cancellationToken)
            ?? throw new KeyNotFoundException("Upload not found.");
        if ((req.FileName ?? string.Empty).Trim() != upload.FileName || req.FileSizeBytes != upload.FileSizeBytes ||
            !string.Equals(req.ContentType, upload.ContentType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Metadata không khớp yêu cầu upload đã cấp.");
        if (upload.AttachmentId is { } existingId)
            return await attachmentStore.GetByIdAsync(existingId, cancellationToken)
                ?? throw new KeyNotFoundException("Attachment not found.");
        if (upload.ConsumedAt.HasValue)
            throw new ArgumentException("Quyền tải tệp đã được sử dụng. Vui lòng tải lại danh sách tệp.");
        if (upload.ExpiresAt <= clock.UtcNow.UtcDateTime)
            throw new ArgumentException("Yêu cầu upload đã hết hạn. Vui lòng tải lại tệp.");

        var bucket = storageService.DefaultBucket;
        var finalKey = $"tickets/{command.TicketId}/attachments/{Guid.NewGuid()}";
        try
        {
            var source = await storageService.StatAsync(bucket, upload.StorageKey, cancellationToken);
            VerifyObject(source, upload);
            await storageService.CopyAsync(bucket, upload.StorageKey, finalKey, source!.ETag, cancellationToken);
            VerifyObject(await storageService.StatAsync(bucket, finalKey, cancellationToken), upload);
        }
        catch (Exception ex) when (ex is not ArgumentException and not OperationCanceledException)
        {
            throw new ServiceUnavailableException("Không thể xác minh tệp đã tải lên. Vui lòng thử xác nhận lại.");
        }

        var attachment = TicketAttachment.Create(command.TicketId, upload.FileName, upload.FileSizeBytes,
            upload.ContentType, finalKey, actorId, clock.UtcNow.UtcDateTime);
        // Không xóa finalKey khi commit báo lỗi: kết quả commit có thể chưa xác định do mất kết nối.
        // Tác vụ dọn object mồ côi phải kiểm tra metadata và thời hạn trước khi xóa.
        TicketAttachmentDto result;
        try
        {
            result = await attachmentStore.ConfirmAsync(upload.Id, attachment, clock.UtcNow.UtcDateTime, cancellationToken);
        }
        catch (Exception error) when (error is ArgumentException or AttachmentConfirmationBusyException)
        {
            // Giao dịch bị từ chối chắc chắn nên có thể dọn bản sao đích và giữ lại nguồn để thử lại.
            await TryCleanupAsync(bucket, finalKey);
            throw;
        }
        await TryCleanupAsync(bucket, upload.StorageKey);
        if (result.StorageKey != finalKey) await TryCleanupAsync(bucket, finalKey);
        return result;
    }

    private static void VerifyObject(StoredObject? value, AttachmentUploadGrant upload)
    {
        if (value is null) throw new ArgumentException("Chưa tìm thấy tệp đã tải lên.");
        if (value.Size != upload.FileSizeBytes || value.Size <= 0 || value.Size > 10 * 1024 * 1024 ||
            string.IsNullOrWhiteSpace(value.ContentType) ||
            !string.Equals(value.ContentType.Split(';')[0].Trim(), upload.ContentType.Split(';')[0].Trim(), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Kích thước hoặc loại tệp thực tế không khớp yêu cầu upload.");
    }

    private async Task TryCleanupAsync(string bucket, string key)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await storageService.DeleteAsync(bucket, key, timeout.Token); }
        catch (Exception) { /* Adapter ghi log; metadata đã commit, cleanup không biến thành lỗi nghiệp vụ. */ }
    }
}
