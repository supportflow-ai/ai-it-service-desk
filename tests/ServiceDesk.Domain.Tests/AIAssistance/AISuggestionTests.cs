using ServiceDesk.Domain.AIAssistance;
using FluentAssertions;
using Xunit;

namespace ServiceDesk.Domain.Tests.AIAssistance;

public class AISuggestionTests
{
    private static AISuggestion CreatePendingSuggestion(double? score = 0.8)
    {
        var analysis = AIAnalysis.CreateSucceeded(
            Guid.NewGuid(), 1, "hash123", DateTimeOffset.UtcNow);

        return AISuggestion.Create(analysis.Id, SuggestionType.Category, "NET", score);
    }

    [Fact]
    public void Create_ValidInput_ShouldReturnPendingSuggestion()
    {
        var suggestion = CreatePendingSuggestion();

        suggestion.Decision.Should().Be(SuggestionDecision.Pending);
        suggestion.SuggestedValue.Should().Be("NET");
        suggestion.ModelScore.Should().Be(0.8);
        suggestion.FinalValue.Should().BeNull();
        suggestion.ReviewedBy.Should().BeNull();
        suggestion.ReviewedAt.Should().BeNull();
        suggestion.IsTerminal.Should().BeFalse();
    }

    [Fact]
    public void Create_InvalidScore_NaN_ShouldThrow()
    {
        var act = () => AISuggestion.Create(Guid.NewGuid(), SuggestionType.Category, "NET", double.NaN);

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("modelScore");
    }

    [Fact]
    public void Create_InvalidScore_Infinity_ShouldThrow()
    {
        var act = () => AISuggestion.Create(Guid.NewGuid(), SuggestionType.Category, "NET", double.PositiveInfinity);

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("modelScore");
    }

    [Fact]
    public void Create_InvalidScore_OutOfRange_ShouldThrow()
    {
        var act = () => AISuggestion.Create(Guid.NewGuid(), SuggestionType.Category, "NET", 1.5);

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("modelScore");
    }

    [Fact]
    public void Create_NullScore_ShouldBeAllowed()
    {
        var suggestion = CreatePendingSuggestion(score: null);

        suggestion.ModelScore.Should().BeNull();
    }

    [Fact]
    public void Create_EmptySuggestedValue_ShouldThrow()
    {
        var act = () => AISuggestion.Create(Guid.NewGuid(), SuggestionType.Category, "", 0.8);

        act.Should().Throw<ArgumentException>();
    }

    // --- Accept ---

    [Fact]
    public void Accept_Pending_ShouldSetFinalValueToSuggestedValue()
    {
        var suggestion = CreatePendingSuggestion();
        var now = DateTimeOffset.UtcNow;

        suggestion.Accept("agent-1", now);

        suggestion.Decision.Should().Be(SuggestionDecision.Accepted);
        suggestion.FinalValue.Should().Be("NET");
        suggestion.ReviewedBy.Should().Be("agent-1");
        suggestion.ReviewedAt.Should().Be(now);
        suggestion.IsTerminal.Should().BeTrue();
    }

    // --- Override ---

    [Fact]
    public void Override_Pending_ShouldSetFinalValueToHumanValue()
    {
        var suggestion = CreatePendingSuggestion();
        var now = DateTimeOffset.UtcNow;

        suggestion.Override("VPN", "agent-1", now);

        suggestion.Decision.Should().Be(SuggestionDecision.Overridden);
        suggestion.FinalValue.Should().Be("VPN");
        suggestion.ReviewedBy.Should().Be("agent-1");
        suggestion.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void Override_EmptyFinalValue_ShouldThrow()
    {
        var suggestion = CreatePendingSuggestion();

        var act = () => suggestion.Override("", "agent-1", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    // --- Reject ---

    [Fact]
    public void Reject_Pending_ShouldSetNullFinalValue()
    {
        var suggestion = CreatePendingSuggestion();

        suggestion.Reject("agent-1", DateTimeOffset.UtcNow);

        suggestion.Decision.Should().Be(SuggestionDecision.Rejected);
        suggestion.FinalValue.Should().BeNull();
        suggestion.IsTerminal.Should().BeTrue();
    }

    // --- Terminal State ---

    [Fact]
    public void Accept_AlreadyAccepted_ShouldThrow()
    {
        var suggestion = CreatePendingSuggestion();
        suggestion.Accept("agent-1", DateTimeOffset.UtcNow);

        var act = () => suggestion.Accept("agent-2", DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Override_AlreadyRejected_ShouldThrow()
    {
        var suggestion = CreatePendingSuggestion();
        suggestion.Reject("agent-1", DateTimeOffset.UtcNow);

        var act = () => suggestion.Override("VPN", "agent-2", DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reject_AlreadyOverridden_ShouldThrow()
    {
        var suggestion = CreatePendingSuggestion();
        suggestion.Override("VPN", "agent-1", DateTimeOffset.UtcNow);

        var act = () => suggestion.Reject("agent-2", DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Accept_ExpiredSuggestion_ShouldThrow()
    {
        var suggestion = CreatePendingSuggestion();
        suggestion.MarkExpired();

        var act = () => suggestion.Accept("agent-1", DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Override_ExpiredSuggestion_ShouldThrow()
    {
        var suggestion = CreatePendingSuggestion();
        suggestion.MarkExpired();

        var act = () => suggestion.Override("VPN", "agent-1", DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    // --- Expire ---

    [Fact]
    public void MarkExpired_Pending_ShouldBecomeExpired()
    {
        var suggestion = CreatePendingSuggestion();

        suggestion.MarkExpired();

        suggestion.Decision.Should().Be(SuggestionDecision.Expired);
        suggestion.FinalValue.Should().BeNull();
        suggestion.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void MarkExpired_AlreadyAccepted_ShouldRemainAccepted()
    {
        var suggestion = CreatePendingSuggestion();
        suggestion.Accept("agent-1", DateTimeOffset.UtcNow);

        suggestion.MarkExpired(); // Should be no-op for terminal state

        suggestion.Decision.Should().Be(SuggestionDecision.Accepted);
    }
}
