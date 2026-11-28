using ServiceDesk.Modules.Ticketing.Application.Contracts;

namespace ServiceDesk.Modules.Ticketing.Application.Ports;

public interface IAttachmentScanner
{
    Task ScanAsync(string key, long expectedSize, string contentType, CancellationToken cancellationToken = default);
    Task<string> PrepareDownloadAsync(TicketAttachmentDto attachment, CancellationToken cancellationToken = default);
}
