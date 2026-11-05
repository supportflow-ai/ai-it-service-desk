using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceDesk.Application.Knowledge.Indexing;
using ServiceDesk.Infrastructure.Options;

namespace ServiceDesk.Infrastructure.Knowledge.Indexing;

internal sealed class KnowledgeIndexWorker(IServiceScopeFactory scopes, IOptions<KnowledgeIndexOptions> options,
    ILogger<KnowledgeIndexWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = options.Value;
        if (!config.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IKnowledgeIndexProcessor>();
                var result = await processor.ProcessNextAsync(TimeSpan.FromSeconds(config.LeaseSeconds),
                    config.MaxAttempts, stoppingToken);
                if (result.Found)
                {
                    logger.LogInformation("knowledge_index_job {JobId} {Outcome}", result.JobId, result.Outcome);
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception)
            {
                // Connection/provider exceptions may contain credentials or article data.
                // A crashed attempt is recovered by its expiring lease, bounded by MaxAttempts.
                logger.LogError("knowledge_index_iteration_failed");
            }
            try { await Task.Delay(TimeSpan.FromSeconds(config.PollSeconds), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
