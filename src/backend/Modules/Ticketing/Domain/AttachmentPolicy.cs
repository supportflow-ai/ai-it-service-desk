namespace ServiceDesk.Modules.Ticketing.Domain;

/// <summary>Hạn mức canonical; giữ các giá trị hiện hành, pending grant chưa phải chính sách sản phẩm đã chốt.</summary>
internal static class AttachmentPolicy
{
    public static string ValidateMetadata(string fileName, long size, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Trim().Length > 255) throw new ArgumentException("File name is required and must not exceed 255 characters.");
        var type = contentType?.Split(';')[0].Trim() ?? string.Empty;
        var allowed = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => type.Equals("application/pdf", StringComparison.OrdinalIgnoreCase),
            ".png" => type.Equals("image/png", StringComparison.OrdinalIgnoreCase),
            ".jpg" or ".jpeg" => type.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase),
            ".docx" => type.Equals("application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.OrdinalIgnoreCase),
            ".txt" => type.Equals("text/plain", StringComparison.OrdinalIgnoreCase),
            ".zip" => type.Equals("application/zip", StringComparison.OrdinalIgnoreCase) || type.Equals("application/x-zip-compressed", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
        if (!allowed) throw new ArgumentException("File type or Content-Type is not allowed. Allowed types: PDF, PNG, JPG, DOCX, TXT, ZIP.");
        if (size <= 0 || size > MaxFileSizeBytes) throw new ArgumentException("File size must be between 1 byte and 10MB.");
        return type;
    }

    public const long MaxFileSizeBytes = 10L * 1024 * 1024;
    public const int MaxFilesPerTicket = 10;
    public const long MaxBytesPerTicket = 50L * 1024 * 1024;
    public const int MaxPendingGrantsPerActor = 20;
}
