namespace ServiceDesk.Modules.Ticketing.Infrastructure;

internal sealed class AttachmentScanningOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 3310;
}
