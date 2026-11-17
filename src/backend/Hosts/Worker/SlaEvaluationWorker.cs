using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceDesk.Modules.Ticketing.Application.EvaluateSla;

namespace ServiceDesk.Worker;

/// <summary>
/// Background worker định kỳ quét và đánh giá vi phạm SLA cho các ticket đang hoạt động.
/// </summary>
public sealed class SlaEvaluationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SlaEvaluationWorker> logger,
    TimeSpan? period = null) : BackgroundService
{
    private readonly TimeSpan _period = period ?? TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SLA Evaluation Worker đã khởi động với chu kỳ {PeriodSeconds}s.", _period.TotalSeconds);

        using var timer = new PeriodicTimer(_period);
        try
        {
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunEvaluationSweepAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("SLA Evaluation Worker đang dừng theo yêu cầu hủy.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi nghiêm trọng không mong muốn trong SLA Evaluation Worker.");
            throw;
        }
    }

    public async Task<SlaEvaluationResult> RunEvaluationSweepAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var useCase = scope.ServiceProvider.GetRequiredService<IEvaluateSla>();
            var result = await useCase.ExecuteAsync(cancellationToken);

            if (result.UpdatedCount > 0 || result.ConflictCount > 0)
            {
                logger.LogInformation(
                    "SLA sweep hoàn tất: Đã duyệt {Processed} tickets, cập nhật vi phạm {Updated}, xung đột {Conflicts}.",
                    result.ProcessedCount, result.UpdatedCount, result.ConflictCount);
            }
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi thực hiện SLA evaluation sweep.");
            return new SlaEvaluationResult(0, 0, 0);
        }
    }
}
