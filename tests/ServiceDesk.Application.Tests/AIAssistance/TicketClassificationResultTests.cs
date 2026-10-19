using ServiceDesk.Application.AIAssistance.Ports;
using ServiceDesk.Application.AIAssistance.Contracts;
using ServiceDesk.Domain.AIAssistance;
using ServiceDesk.Domain.Ticketing;
using FluentAssertions;
using Xunit;

namespace ServiceDesk.Application.Tests.AIAssistance;

public class TriageResultTests
{
    [Fact]
    public void Success_ShouldHaveCorrectOutcome()
    {
        var result = TriageResult.Success(
            TicketCategory.NET, Impact.High, Urgency.Medium,
            categoryScore: 0.85, impactScore: 0.7, urgencyScore: 0.6);

        result.Outcome.Should().Be(TriageOutcome.Success);
        result.SuggestedCategory.Should().Be(TicketCategory.NET);
        result.SuggestedImpact.Should().Be(Impact.High);
        result.SuggestedUrgency.Should().Be(Urgency.Medium);
        result.CategoryScore.Should().Be(0.85);
        result.ImpactScore.Should().Be(0.7);
        result.UrgencyScore.Should().Be(0.6);
        result.CategoryConfidence.Should().Be(ConfidenceBand.High);
        result.ImpactConfidence.Should().Be(ConfidenceBand.Medium);
        result.UrgencyConfidence.Should().Be(ConfidenceBand.Medium);
    }

    [Fact]
    public void Unavailable_ShouldHaveControlledState()
    {
        var result = TriageResult.Unavailable();

        result.Outcome.Should().Be(TriageOutcome.Unavailable);
        result.SuggestedCategory.Should().BeNull();
        result.SuggestedImpact.Should().BeNull();
        result.SuggestedUrgency.Should().BeNull();
        result.CategoryScore.Should().BeNull();
        result.FailureReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Timeout_ShouldHaveControlledState()
    {
        var result = TriageResult.Timeout();

        result.Outcome.Should().Be(TriageOutcome.Timeout);
        result.FailureReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void InvalidResponse_ShouldCarryValidationErrors()
    {
        var errors = new List<string> { "Unknown category: DESKTOP", "Score out of range" };
        var result = TriageResult.InvalidResponse("Bad output", errors);

        result.Outcome.Should().Be(TriageOutcome.InvalidResponse);
        result.ValidationErrors.Should().HaveCount(2);
        result.FailureReason.Should().Be("Bad output");
    }

    [Fact]
    public void RequiresManualTriage_SEC_ShouldBeTrue()
    {
        var result = TriageResult.Success(TicketCategory.SEC, null, null);

        result.RequiresManualTriage.Should().BeTrue();
    }

    [Fact]
    public void RequiresManualTriage_NET_ShouldBeFalse()
    {
        var result = TriageResult.Success(TicketCategory.NET, null, null);

        result.RequiresManualTriage.Should().BeFalse();
    }

    [Fact]
    public void ConfidenceBand_NullScore_ShouldReturnNull()
    {
        var result = TriageResult.Success(TicketCategory.NET, null, null);

        result.CategoryConfidence.Should().BeNull();
    }


}

public class TriageRequestTests
{
    [Fact]
    public void TriageRequest_ShouldCaptureTicketSnapshot()
    {
        var ticketId = Guid.NewGuid();
        var request = new TriageRequest
        {
            TicketId = ticketId,
            TicketRevision = 42,
            Title = "VPN not connecting",
            Description = "Cannot access corporate network via VPN"
        };

        request.TicketId.Should().Be(ticketId);
        request.TicketRevision.Should().Be(42);
        request.Title.Should().Be("VPN not connecting");
        request.Description.Should().Be("Cannot access corporate network via VPN");
    }
}

public class RequesterTriageViewTests
{
    [Fact]
    public void FromTriageResult_ShouldExcludeImpactUrgencyAndRawScores()
    {
        var result = TriageResult.Success(
            TicketCategory.NET, Impact.High, Urgency.Medium,
            categoryScore: 0.85, impactScore: 0.7, urgencyScore: 0.6);

        var view = RequesterTriageView.FromTriageResult(result);

        view.SuggestedCategory.Should().Be(TicketCategory.NET);
        view.CategoryConfidence.Should().Be(ConfidenceBand.High);
        view.Outcome.Should().Be(TriageOutcome.Success);

        // These fields MUST NOT exist on RequesterTriageView
        view.GetType().GetProperty("SuggestedImpact").Should().BeNull();
        view.GetType().GetProperty("SuggestedUrgency").Should().BeNull();
        view.GetType().GetProperty("CategoryScore").Should().BeNull();
        view.GetType().GetProperty("ImpactScore").Should().BeNull();
        view.GetType().GetProperty("UrgencyScore").Should().BeNull();
    }
}

public class AgentTriageViewTests
{
    [Fact]
    public void FromTriageResult_ShouldIncludeAllFields()
    {
        var result = TriageResult.Success(
            TicketCategory.NET, Impact.High, Urgency.Medium,
            categoryScore: 0.85, impactScore: 0.7, urgencyScore: 0.6);

        var view = AgentTriageView.FromTriageResult(result);

        view.SuggestedCategory.Should().Be(TicketCategory.NET);
        view.SuggestedImpact.Should().Be(Impact.High);
        view.SuggestedUrgency.Should().Be(Urgency.Medium);
        view.CategoryScore.Should().Be(0.85);
        view.ImpactScore.Should().Be(0.7);
        view.UrgencyScore.Should().Be(0.6);
        view.CategoryConfidence.Should().Be(ConfidenceBand.High);
    }
}
