using ServiceDesk.SharedKernel.Application;
using ServiceDesk.Modules.Ticketing.Domain;
namespace ServiceDesk.Modules.Ticketing.Domain;

/// <summary>
/// Quyền một lần do máy chủ cấp trước khi client tải tệp trực tiếp lên object storage.
/// </summary>
public sealed class AttachmentUploadGrant
{
    private AttachmentUploadGrant() { }

    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid ActorId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public string ContentType { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ConsumedAt { get; private set; }
    public Guid? AttachmentId { get; private set; }

    // Giữ để biểu diễn các grant cũ đã consume nhưng chưa có liên kết attachment.
    public void Consume(DateTime now)
    {
        if (ConsumedAt.HasValue || ExpiresAt <= now)
            throw new RuleViolationException("attachment_upload_unusable", "Quyền tải tệp đã được sử dụng hoặc hết hạn.");
        ConsumedAt = now;
    }

    public void Confirm(TicketAttachment attachment, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        if (ConsumedAt.HasValue || ExpiresAt <= now)
            throw new RuleViolationException("attachment_upload_unusable", "Quyền tải tệp đã được sử dụng hoặc hết hạn.");
        if (attachment.TicketId != TicketId || attachment.UploadedBy != ActorId ||
            attachment.FileName != FileName || attachment.FileSizeBytes != FileSizeBytes ||
            attachment.ContentType != ContentType ||
            !attachment.StorageKey.StartsWith($"tickets/{TicketId}/attachments/", StringComparison.Ordinal))
            throw new RuleViolationException("attachment_metadata_mismatch", "Metadata không khớp quyền tải tệp đã cấp.");
        ConsumedAt = now;
        AttachmentId = attachment.Id;
    }

    public static AttachmentUploadGrant CreatePending(Guid ticketId, Guid actorId, string fileName,
        long fileSizeBytes, string contentType, DateTime expiresAt) =>
        Create(ticketId, actorId, fileName, fileSizeBytes, contentType,
            $"uploads/pending/{ticketId}/{Guid.NewGuid()}", expiresAt);

    public static AttachmentUploadGrant Create(
        Guid ticketId,
        Guid actorId,
        string fileName,
        long fileSizeBytes,
        string contentType,
        string storageKey,
        DateTime expiresAt)
    {
        if (ticketId == Guid.Empty) throw new InvalidInputException("ticket_id_required", "Cần có mã phiếu.");
        if (actorId == Guid.Empty) throw new InvalidInputException("actor_id_required", "Cần có mã người thực hiện.");
        if (string.IsNullOrWhiteSpace(fileName)) throw new InvalidInputException("attachment_file_name_required", "Cần có tên tệp.");
        if (fileSizeBytes <= 0 || fileSizeBytes > 10 * 1024 * 1024)
            throw TicketErrors.FileSizeInvalid();
        if (string.IsNullOrWhiteSpace(contentType)) throw new InvalidInputException("attachment_content_type_required", "Cần có loại nội dung (content type).");
        if (string.IsNullOrWhiteSpace(storageKey)) throw new InvalidInputException("attachment_storage_key_required", "Cần có storageKey.");
        if (fileName.Trim().Length > 255 || contentType.Trim().Length > 100 || storageKey.Trim().Length > 500)
            throw new InvalidInputException("attachment_metadata_too_long", "Metadata vượt quá độ dài cho phép.");

        return new AttachmentUploadGrant
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            ActorId = actorId,
            FileName = fileName.Trim(),
            FileSizeBytes = fileSizeBytes,
            ContentType = contentType.Trim(),
            StorageKey = storageKey.Trim(),
            ExpiresAt = expiresAt
        };
    }
}
