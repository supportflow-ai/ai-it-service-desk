using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ServiceDesk.Modules.Ticketing.Application.EvaluateSla;
using ServiceDesk.Worker;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Unit")]
public sealed class SlaEvaluationWorkerHostTests
{
    [Fact]
    public async Task UnexpectedSweepFailureIsNotConvertedToSuccessfulZeroWork()
    {
        _evaluateSla.Setup(e => e.ExecuteAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("synthetic outage"));
        using var worker = new SlaEvaluationWorker(_scopeFactory.Object, _logger.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => worker.RunEvaluationSweepAsync(CancellationToken.None));
        Assert.Equal("Failure", worker.Snapshot.State);
        Assert.Null(worker.Snapshot.LastSuccessfulAt);
        Assert.Null(worker.Snapshot.Result);
    }

    [Fact]
    public async Task AlreadyCancelledSweepDoesNotResolveOrRunWork()
    {
        _evaluateSla.Setup(e => e.ExecuteAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new SlaEvaluationResult(0, 0, 0));
        using var stop = new CancellationTokenSource();
        stop.Cancel();
        using var worker = new SlaEvaluationWorker(_scopeFactory.Object, _logger.Object);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker.RunEvaluationSweepAsync(stop.Token));
        _evaluateSla.Verify(e => e.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConcurrentSweepCallsNeverOverlap()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var maximum = 0;
        _evaluateSla.Setup(e => e.ExecuteAsync(It.IsAny<CancellationToken>())).Returns(async () =>
        {
            var current = Interlocked.Increment(ref active);
            maximum = Math.Max(maximum, current);
            await release.Task;
            Interlocked.Decrement(ref active);
            return new SlaEvaluationResult(1, 0, 0);
        });
        using var worker = new SlaEvaluationWorker(_scopeFactory.Object, _logger.Object);
        var first = worker.RunEvaluationSweepAsync(CancellationToken.None);
        var second = worker.RunEvaluationSweepAsync(CancellationToken.None);
        release.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, maximum);
    }

    [Fact]
    public async Task FailurePreservesLastSuccessAndNoWorkRecordsARealSuccessfulSweep()
    {
        var now = new DateTimeOffset(2026, 11, 28, 0, 0, 0, TimeSpan.Zero);
        var clock = new Mock<ServiceDesk.SharedKernel.Application.Abstractions.IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(() => now);
        _evaluateSla.SetupSequence(e => e.ExecuteAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlaEvaluationResult(2, 1, 0))
            .ThrowsAsync(new InvalidOperationException("sensitive synthetic failure"))
            .ReturnsAsync(new SlaEvaluationResult(0, 0, 0));
        using var worker = new SlaEvaluationWorker(_scopeFactory.Object, _logger.Object, clock: clock.Object);
        await worker.RunEvaluationSweepAsync(default);
        var success = worker.Snapshot.LastSuccessfulAt;
        now = now.AddMinutes(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => worker.RunEvaluationSweepAsync(default));
        Assert.Equal(success, worker.Snapshot.LastSuccessfulAt);
        Assert.Equal(nameof(InvalidOperationException), worker.Snapshot.FailureType);
        now = now.AddMinutes(1);
        await worker.RunEvaluationSweepAsync(default);
        Assert.Equal("NoWork", worker.Snapshot.State);
        Assert.Equal(now, worker.Snapshot.LastSuccessfulAt);
        Assert.Null(worker.Snapshot.FailureType);
    }

    [Fact]
    public async Task HostedLoopRetriesAnOutageAndStopsAfterRecovery()
    {
        var logger = new RecoveryLogger();
        _evaluateSla.SetupSequence(e => e.ExecuteAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("synthetic outage"))
            .ReturnsAsync(new SlaEvaluationResult(0, 0, 0));
        using var worker = new SlaEvaluationWorker(_scopeFactory.Object, logger, TimeSpan.FromMilliseconds(5));
        await worker.StartAsync(default);
        await logger.Recovered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(default);
        Assert.NotNull(worker.Snapshot.LastSuccessfulAt);
        Assert.Contains(logger.Messages, m => m.Contains("thử lại sau", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, m => m.Contains("synthetic outage", StringComparison.Ordinal));
    }

    private sealed class RecoveryLogger : ILogger<SlaEvaluationWorker>
    {
        public TaskCompletionSource Recovered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public System.Collections.Concurrent.ConcurrentQueue<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var text = formatter(state, exception);
            Messages.Enqueue(text);
            if (text.StartsWith("SLA sweep NoWork", StringComparison.Ordinal)) Recovered.TrySetResult();
        }
    }

    private readonly Mock<IServiceScopeFactory> _scopeFactory = new(MockBehavior.Strict);
    private readonly Mock<IServiceScope> _scope = new(MockBehavior.Strict);
    private readonly Mock<IServiceProvider> _serviceProvider = new(MockBehavior.Strict);
    private readonly Mock<IEvaluateSla> _evaluateSla = new(MockBehavior.Strict);
    private readonly Mock<ILogger<SlaEvaluationWorker>> _logger = new();

    public SlaEvaluationWorkerHostTests()
    {
        _scopeFactory.Setup(f => f.CreateScope()).Returns(_scope.Object);
        _scope.SetupGet(s => s.ServiceProvider).Returns(_serviceProvider.Object);
        _scope.Setup(s => s.Dispose());
        _serviceProvider.Setup(p => p.GetService(typeof(IEvaluateSla))).Returns(_evaluateSla.Object);
    }

    [Fact]
    public async Task RunEvaluationSweepAsync_ResolvesScope_AndReturnsResult()
    {
        _evaluateSla.Setup(e => e.ExecuteAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlaEvaluationResult(10, 2, 0));

        var worker = new SlaEvaluationWorker(_scopeFactory.Object, _logger.Object, TimeSpan.FromMilliseconds(50));

        var result = await worker.RunEvaluationSweepAsync(CancellationToken.None);

        Assert.Equal(10, result.ProcessedCount);
        Assert.Equal(2, result.UpdatedCount);
        Assert.Equal(0, result.ConflictCount);
        _evaluateSla.Verify(e => e.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Once);
        _scope.Verify(s => s.Dispose(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_StopsGracefully_WhenCancellationTokenIsCancelled()
    {
        _evaluateSla.Setup(e => e.ExecuteAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlaEvaluationResult(0, 0, 0));

        using var cts = new CancellationTokenSource();
        var worker = new SlaEvaluationWorker(_scopeFactory.Object, _logger.Object, TimeSpan.FromMilliseconds(20));

        var task = worker.StartAsync(cts.Token);
        await Task.Delay(30);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        await task;
    }
}
