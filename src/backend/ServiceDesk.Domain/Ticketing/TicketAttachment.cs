namespace ServiceDesk.Domain.Ticketing;

/// <summary>
/// Thực thể lưu thông tin metadata của tệp đính kèm trên Ticket.
/// Nội dung nhị phân thực tế được lưu trên MinIO/S3 object storage.
/// </summary>
public sealed class TicketAttachment
{
    private TicketAttachment() { }

    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public string ContentType { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public Guid UploadedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static TicketAttachment Create(
        Guid ticketId,
        string fileName,
        long fileSizeBytes,
        string contentType,
        string storageKey,
        Guid uploadedBy,
        DateTime now)
    {
        if (ticketId == Guid.Empty) throw new ArgumentException("Ticket ID is required.");
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("File name is required.");
        if (fileSizeBytes <= 0) throw new ArgumentException("File size must be positive.");
        if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("Storage key is required.");
        if (uploadedBy == Guid.Empty) throw new ArgumentException("Uploader ID is required.");

        return new TicketAttachment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            FileName = fileName.Trim(),
            FileSizeBytes = fileSizeBytes,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim(),
            StorageKey = storageKey.Trim(),
            UploadedBy = uploadedBy,
            CreatedAt = now
        };
    }
}
