using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceDesk.Modules.Ticketing.Application.EvaluateSla;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Worker;

/// <summary>
/// Background worker định kỳ quét và đánh giá vi phạm SLA cho các ticket đang hoạt động.
/// </summary>
public sealed class SlaEvaluationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SlaEvaluationWorker> logger,
    TimeSpan? period = null,
    IClock? clock = null) : BackgroundService
{
    private readonly TimeSpan _period = period ?? TimeSpan.FromSeconds(60);
    private readonly SemaphoreSlim _sweep = new(1, 1);
    private SlaWorkerSnapshot _snapshot = new("NotRun", null, null, null, null, null);
    public SlaWorkerSnapshot Snapshot => Volatile.Read(ref _snapshot);
    private DateTimeOffset UtcNow => clock?.UtcNow ?? DateTimeOffset.UtcNow;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SLA Evaluation Worker đã khởi động với chu kỳ {PeriodSeconds}s.", _period.TotalSeconds);

        using var timer = new PeriodicTimer(_period);
        var failures = 0;
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var result = await RunEvaluationSweepAsync(stoppingToken);
                    failures = result.FailedCount == 0 ? 0 : Math.Min(failures + 1, 3);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                catch (Exception) { failures = Math.Min(failures + 1, 3); }
                timer.Period = TimeSpan.FromMilliseconds(Math.Min(
                    _period.TotalMilliseconds * (1 << failures), Math.Max(_period.TotalMilliseconds, TimeSpan.FromMinutes(5).TotalMilliseconds)));
                if (failures > 0) logger.LogWarning("SLA sweep sẽ thử lại sau {DelaySeconds}s.", timer.Period.TotalSeconds);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("SLA Evaluation Worker đang dừng theo yêu cầu hủy.");
        }
    }

    public async Task<SlaEvaluationResult> RunEvaluationSweepAsync(CancellationToken cancellationToken)
    {
        await _sweep.WaitAsync(cancellationToken);
        var previous = Snapshot;
        var started = UtcNow;
        Volatile.Write(ref _snapshot, new("Running", started, null, previous.LastSuccessfulAt, null, null));
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var useCase = scope.ServiceProvider.GetRequiredService<IEvaluateSla>();
            var result = await useCase.ExecuteAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var completed = UtcNow;
            Volatile.Write(ref _snapshot, new(result.Outcome, started, completed,
                result.FailedCount == 0 ? completed : previous.LastSuccessfulAt, result, null));
            logger.LogInformation("SLA sweep {Outcome}: processed {Processed}, updated {Updated}, conflicts {Conflicts}, failed {Failed}.",
                result.Outcome, result.ProcessedCount, result.UpdatedCount, result.ConflictCount, result.FailedCount);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Volatile.Write(ref _snapshot, new("Cancelled", started, UtcNow, previous.LastSuccessfulAt, null, null));
            throw;
        }
        catch (Exception ex)
        {
            Volatile.Write(ref _snapshot, new("Failure", started, UtcNow, previous.LastSuccessfulAt, null, ex.GetType().Name));
            logger.LogError("SLA sweep thất bại ({FailureType}).", ex.GetType().Name);
            throw;
        }
        finally { _sweep.Release(); }
    }
}
