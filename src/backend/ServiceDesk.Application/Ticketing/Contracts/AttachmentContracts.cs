namespace ServiceDesk.Application.Ticketing.Contracts;

public record GenerateUploadUrlRequest(string FileName, long FileSizeBytes, string ContentType);

public record UploadUrlResult(string UploadUrl, string StorageKey, int ExpirySeconds);

public record DownloadUrlResult(string DownloadUrl);
