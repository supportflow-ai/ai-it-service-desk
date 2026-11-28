using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;

namespace ServiceDesk.IntegrationTests;

// Chỉ dùng cho test quyền/TTL; test bảo mật quét dùng adapter thật.
internal sealed class CleanAttachmentScannerStub : IAttachmentScanner
{
    public Task ScanAsync(string key, long expectedSize, string contentType, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<string> PrepareDownloadAsync(TicketAttachmentDto attachment, CancellationToken cancellationToken = default) => Task.FromResult(attachment.StorageKey);
}
