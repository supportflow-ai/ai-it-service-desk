using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceDesk.Modules.Ticketing.Application.AutoCloseTickets;

namespace ServiceDesk.Worker;

/// <summary>
/// Background worker định kỳ quét và tự động đóng các ticket ở trạng thái Resolved sau 72 giờ không phản hồi (DEC-05).
/// </summary>
public sealed class AutoCloseResolvedTicketsWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AutoCloseResolvedTicketsWorker> logger,
    TimeSpan? period = null) : BackgroundService
{
    private readonly TimeSpan _period = period ?? TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Auto-Close Resolved Tickets Worker đã khởi động với chu kỳ {PeriodHours}h.", _period.TotalHours);

        using var timer = new PeriodicTimer(_period);
        try
        {
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunAutoCloseSweepAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Auto-Close Resolved Tickets Worker đang dừng theo yêu cầu hủy.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi nghiêm trọng không mong muốn trong Auto-Close Resolved Tickets Worker.");
            throw;
        }
    }

    public async Task<AutoCloseResult> RunAutoCloseSweepAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var useCase = scope.ServiceProvider.GetRequiredService<IAutoCloseTickets>();
            var result = await useCase.ExecuteAsync(cancellationToken: cancellationToken);

            if (result.ClosedCount > 0 || result.ConflictCount > 0)
            {
                logger.LogInformation(
                    "Auto-Close sweep hoàn tất: Đã kiểm tra {Processed} tickets, đóng {Closed}, xung đột {Conflicts}.",
                    result.ProcessedCount, result.ClosedCount, result.ConflictCount);
            }
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi trong lần quét Auto-Close Resolved Tickets.");
            return new AutoCloseResult(0, 0, 0);
        }
    }
}
