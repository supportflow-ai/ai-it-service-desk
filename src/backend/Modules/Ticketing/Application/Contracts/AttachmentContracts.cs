namespace ServiceDesk.Modules.Ticketing.Application.Contracts;

public record GenerateUploadUrlRequest(string FileName, long FileSizeBytes, string ContentType);

public record UploadUrlResult(string UploadUrl, string StorageKey, int ExpirySeconds, IReadOnlyDictionary<string, string>? FormData = null);

public record DownloadUrlResult(string DownloadUrl);

public record ConfirmAttachmentUploadRequest(string FileName, long FileSizeBytes, string ContentType, string StorageKey);

public record TicketAttachmentDto(
    Guid Id,
    Guid TicketId,
    string FileName,
    long FileSizeBytes,
    string ContentType,
    string StorageKey,
    Guid UploadedBy,
    DateTime CreatedAt
);
