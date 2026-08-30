using ServiceDesk.Application.AIAssistance.Interfaces;
using FluentAssertions;
using Xunit;

namespace ServiceDesk.Application.Tests.AIAssistance;

public class TicketClassificationResultTests
{
    [Fact]
    public void Empty_ShouldReturnResultWithZeroConfidence()
    {
        var result = TicketClassificationResult.Empty;

        result.SuggestedCategory.Should().BeNull();
        result.SuggestedImpact.Should().BeNull();
        result.SuggestedUrgency.Should().BeNull();
        result.Confidence.Should().Be(0);
    }
}
