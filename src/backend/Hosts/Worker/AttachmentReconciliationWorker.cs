using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceDesk.Modules.Ticketing.Application.ReconcileAttachments;

namespace ServiceDesk.Worker;

public sealed class AttachmentReconciliationOptions
{
    public int? OrphanRetentionHours { get; set; }
}

public sealed class AttachmentReconciliationWorker(
    IServiceScopeFactory scopes,
    IOptions<AttachmentReconciliationOptions> options,
    ILogger<AttachmentReconciliationWorker> logger) : BackgroundService
{
    private readonly SemaphoreSlim _sweep = new(1, 1);
    private readonly Dictionary<string, string?> _cursors = new()
    {
        ["uploads/pending/"] = null, ["tickets/"] = null, ["downloads/quarantine/"] = null
    };
    private string _status = "NotRun";
    public string Status => Volatile.Read(ref _status);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        try
        {
            do
            {
                try { await RunSweepAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                catch (Exception error) { logger.LogError("Attachment reconciliation thất bại ({FailureType}).", error.GetType().Name); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public async Task<AttachmentReconciliationResult?> RunSweepAsync(CancellationToken cancellationToken = default)
    {
        await _sweep.WaitAsync(cancellationToken);
        try
        {
            if (options.Value.OrphanRetentionHours is not { } hours)
            {
                Volatile.Write(ref _status, "Disabled");
                logger.LogInformation("Attachment reconciliation disabled: chưa cấu hình retention đã được duyệt.");
                return null;
            }
            Volatile.Write(ref _status, "Running");
            var examined = 0; var deleted = 0; var failed = 0; var pruned = 0;
            await using var scope = scopes.CreateAsyncScope();
            var useCase = scope.ServiceProvider.GetRequiredService<IReconcileAttachments>();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            foreach (var prefix in _cursors.Keys.ToArray())
            {
                var result = await useCase.ExecuteAsync(new(prefix, _cursors[prefix], TimeSpan.FromHours(hours)), timeout.Token);
                _cursors[prefix] = result.ContinuationToken;
                examined += result.Examined; deleted += result.Deleted; failed += result.Failed; pruned += result.Pruned;
            }
            cancellationToken.ThrowIfCancellationRequested();
            Volatile.Write(ref _status, failed > 0 ? "PartialFailure" : examined == 0 && pruned == 0 ? "NoWork" : "Success");
            logger.LogInformation("Attachment reconciliation {Outcome}: examined {Examined}, deleted {Deleted}, failed {Failed}, pruned {Pruned}.", Status, examined, deleted, failed, pruned);
            return new(examined, deleted, failed, pruned, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Volatile.Write(ref _status, "Cancelled"); throw;
        }
        catch (Exception)
        {
            Volatile.Write(ref _status, "Failure"); throw;
        }
        finally { _sweep.Release(); }
    }
}
