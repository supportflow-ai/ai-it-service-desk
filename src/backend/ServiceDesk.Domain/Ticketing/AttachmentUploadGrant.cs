namespace ServiceDesk.Domain.Ticketing;

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

    public void Consume(DateTime now) => ConsumedAt = now;

    public static AttachmentUploadGrant Create(
        Guid ticketId,
        Guid actorId,
        string fileName,
        long fileSizeBytes,
        string contentType,
        string storageKey,
        DateTime expiresAt)
    {
        if (ticketId == Guid.Empty) throw new ArgumentException("Ticket ID is required.");
        if (actorId == Guid.Empty) throw new ArgumentException("Actor ID is required.");
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("File name is required.");
        if (fileSizeBytes <= 0) throw new ArgumentException("File size must be positive.");
        if (string.IsNullOrWhiteSpace(contentType)) throw new ArgumentException("Content type is required.");
        if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("Storage key is required.");

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
