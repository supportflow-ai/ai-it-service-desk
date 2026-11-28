namespace ServiceDesk.Modules.Ticketing.Domain;

/// <summary>Hạn mức canonical; giữ các giá trị hiện hành, pending grant chưa phải chính sách sản phẩm đã chốt.</summary>
internal static class AttachmentPolicy
{
    public const long MaxFileSizeBytes = 10L * 1024 * 1024;
    public const int MaxFilesPerTicket = 10;
    public const long MaxBytesPerTicket = 50L * 1024 * 1024;
    public const int MaxPendingGrantsPerActor = 20;
}
