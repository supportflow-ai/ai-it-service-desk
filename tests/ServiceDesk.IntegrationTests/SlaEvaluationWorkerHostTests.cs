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
