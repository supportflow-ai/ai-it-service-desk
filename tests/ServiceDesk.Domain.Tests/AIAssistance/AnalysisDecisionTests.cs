using FluentAssertions;
using ServiceDesk.Modules.AIAssistance.Domain;
using Xunit;

namespace ServiceDesk.Domain.Tests.AIAssistance;

/// <summary>G2-02, G2-05: một analysis nhận đúng một quyết định, kèm bằng chứng trước và sau.</summary>
public class AnalysisDecisionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 28, 8, 0, 0, TimeSpan.Zero);
    private static readonly TriageValues Before = new("VPN", "Medium", "Medium", "P3");
    private static readonly TriageValues After = new("NET", "High", "High", "P1");

    private static AIAnalysis Analysis()
    {
        var analysis = AIAnalysis.CreateSucceeded(Guid.NewGuid(), 1, new string('a', 64), Now);
        analysis.AddSuggestion(AISuggestion.Create(analysis.Id, SuggestionType.Category, "NET", 0.9));
        analysis.AddSuggestion(AISuggestion.Create(analysis.Id, SuggestionType.Impact, "High", 0.8));
        return analysis;
    }

    [Fact]
    public void RecordDecision_Accept_KeepsOriginalSuggestionsAndTicketValues()
    {
        var analysis = Analysis();
        var reviewer = Guid.NewGuid();

        analysis.RecordDecision(SuggestionDecision.Accepted, reviewer, Now, previous: Before, applied: After);

        analysis.Decision.Should().Be(SuggestionDecision.Accepted);
        analysis.DecidedBy.Should().Be(reviewer);
        analysis.DecidedAt.Should().Be(Now);
        analysis.Previous.Should().Be(Before);
        analysis.Applied.Should().Be(After);
        analysis.Suggestions.Should().OnlyContain(s => s.Decision == SuggestionDecision.Accepted && s.FinalValue == s.SuggestedValue);
    }

    [Fact]
    public void RecordDecision_SecondTime_IsRejectedAndHistoryUnchanged()
    {
        var analysis = Analysis();
        var reviewer = Guid.NewGuid();
        analysis.RecordDecision(SuggestionDecision.Rejected, reviewer, Now, "Sai");

        var act = () => analysis.RecordDecision(SuggestionDecision.Accepted, Guid.NewGuid(), Now, previous: Before, applied: After);

        act.Should().Throw<InvalidOperationException>();
        analysis.Decision.Should().Be(SuggestionDecision.Rejected);
        analysis.DecidedBy.Should().Be(reviewer);
        analysis.Applied.Should().BeNull();
    }

    [Theory]
    [InlineData(SuggestionDecision.Pending, false, "lý do")]
    [InlineData(SuggestionDecision.Expired, false, "lý do")]
    [InlineData(SuggestionDecision.Accepted, false, null)]
    [InlineData(SuggestionDecision.Overridden, true, " ")]
    [InlineData(SuggestionDecision.Rejected, true, "lý do")]
    [InlineData(SuggestionDecision.Rejected, false, null)]
    public void RecordDecision_WithInconsistentInput_ChangesNothing(SuggestionDecision decision, bool withValues, string? reason)
    {
        var analysis = Analysis();

        var act = () => analysis.RecordDecision(decision, Guid.NewGuid(), Now, reason,
            withValues ? Before : null, withValues ? After : null);

        act.Should().Throw<ArgumentException>();
        analysis.Decision.Should().Be(SuggestionDecision.Pending);
        analysis.Suggestions.Should().OnlyContain(s => s.Decision == SuggestionDecision.Pending);
    }

    [Fact]
    public void RecordDecision_OnFailedAnalysis_IsRejected()
    {
        var analysis = AIAnalysis.CreateFailed(Guid.NewGuid(), 1, new string('a', 64), Now, "ai_timeout");

        var act = () => analysis.RecordDecision(SuggestionDecision.Rejected, Guid.NewGuid(), Now, "Sai");

        act.Should().Throw<InvalidOperationException>();
    }
}
