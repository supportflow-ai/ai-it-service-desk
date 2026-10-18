using ServiceDesk.Application.AIAssistance.Ports;
using ServiceDesk.Application.AIAssistance.Contracts;
using ServiceDesk.Domain.AIAssistance;
using ServiceDesk.Infrastructure.AI;
using ServiceDesk.Infrastructure.AI.Contracts.V1;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Unit")]
public sealed class ClassificationAdapterContractTests
{
    private static TriageRequest Snapshot() => new()
    { TicketId = Guid.NewGuid(), TicketRevision = 1, Title = "VPN issue", Description = "Cannot connect" };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdaptersPreserveOutcomePostconditionsAndCancellation(bool fake)
    {
        ITicketClassificationService service = fake ? new OfflineAdapter() : new NoOpTicketClassificationService();
        var result = await service.ClassifyAsync(Snapshot());
        Assert.True(Enum.IsDefined(result.Outcome));
        Assert.Null(result.CategoryScore);
        if (result.Outcome != TriageOutcome.Success)
        {
            Assert.NotNull(result.FailureReason);
            Assert.Null(result.SuggestedCategory);
        }
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ClassifyAsync(Snapshot(), cancelled.Token));
    }

    private sealed class OfflineAdapter : ITicketClassificationService
    {
        public Task<TriageResult> ClassifyAsync(TriageRequest snapshot, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = ClassificationContractMapper.ToRequest(snapshot, Guid.NewGuid());
            return Task.FromResult(ClassificationContractMapper.Map(request,
                new ClassificationResponse(request.RequestId, request.TicketId, request.TicketRevision,
                    request.InputHash, [new("category", "VPN", null)])));
        }
    }
}
