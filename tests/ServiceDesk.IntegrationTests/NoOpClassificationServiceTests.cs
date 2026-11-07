using ServiceDesk.Modules.AIAssistance.Application.Ports;
using ServiceDesk.Modules.AIAssistance.Application.Contracts;
using ServiceDesk.Modules.AIAssistance.Domain;
using ServiceDesk.Modules.Ticketing.Domain;
using FluentAssertions;
using Xunit;

namespace ServiceDesk.IntegrationTests;

public class NoOpClassificationServiceTests
{
    [Fact]
    public async Task ClassifyAsync_ShouldReturnUnavailable_NotThrow()
    {
        // Arrange â€” use Infrastructure NoOp via interface
        ITicketClassificationService service = new ServiceDesk.Modules.AIAssistance.Infrastructure.NoOpTicketClassificationService();
        var request = new TriageRequest
        {
            TicketId = Guid.NewGuid(),
            TicketRevision = 1,
            Title = "Test",
            Description = "Test desc"
        };

        // Act
        var result = await service.ClassifyAsync(request, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(TriageOutcome.Unavailable);
        result.SuggestedCategory.Should().BeNull();
        result.CategoryScore.Should().BeNull();
        result.FailureReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ClassifyAsync_ShouldPropagateCancellationToken()
    {
        ITicketClassificationService service = new ServiceDesk.Modules.AIAssistance.Infrastructure.NoOpTicketClassificationService();
        using var cts = new CancellationTokenSource();
        var request = new TriageRequest
        {
            TicketId = Guid.NewGuid(),
            TicketRevision = 1,
            Title = "Test",
            Description = "Test"
        };

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ClassifyAsync(request, cts.Token));
    }
}

