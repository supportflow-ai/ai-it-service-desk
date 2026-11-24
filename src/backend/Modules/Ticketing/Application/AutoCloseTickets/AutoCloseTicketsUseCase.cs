using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.Ticketing.Application.AutoCloseTickets;

public record AutoCloseResult(int ProcessedCount, int ClosedCount, int ConflictCount);

public interface IAutoCloseTickets
{
    Task<AutoCloseResult> ExecuteAsync(TimeSpan? idleThreshold = null, CancellationToken cancellationToken = default);
}

internal sealed class AutoCloseTicketsUseCase(
    IAutoCloseTicketsGateway gateway,
    IClock clock,
    int batchSize = 100) : IAutoCloseTickets
{
    public static readonly TimeSpan DefaultThreshold = TimeSpan.FromHours(72);

    public async Task<AutoCloseResult> ExecuteAsync(TimeSpan? idleThreshold = null, CancellationToken cancellationToken = default)
    {
        var threshold = idleThreshold ?? DefaultThreshold;
        var now = clock.UtcNow.UtcDateTime;
        var cutoff = now - threshold;

        Guid? cursor = null;
        int processed = 0;
        int closed = 0;
        int conflicts = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await gateway.GetResolvedTicketsEligibleForAutoCloseAsync(cutoff, cursor, batchSize, cancellationToken);
            if (batch.Count == 0) break;

            foreach (var ticket in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();
                cursor = ticket.Id;
                processed++;

                const string audit = "Phiếu đã tự động đóng sau 72 giờ giải quyết mà không có phản hồi thêm.";
                var success = await gateway.CloseWithAuditAsync(ticket, now, audit, cancellationToken);
                if (success)
                    closed++;
                else
                    conflicts++;
            }
        }

        return new AutoCloseResult(processed, closed, conflicts);
    }
}
