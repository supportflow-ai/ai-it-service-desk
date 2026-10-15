using ServiceDesk.Domain.AIAssistance;
using FluentAssertions;
using Xunit;

namespace ServiceDesk.Domain.Tests.AIAssistance;

public class AIAnalysisTests
{
    [Fact]
    public void CreateSucceeded_ShouldStoreTicketSnapshot()
    {
        var ticketId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var analysis = AIAnalysis.CreateSucceeded(ticketId, 5, "abc123", now);

        analysis.TicketId.Should().Be(ticketId);
        analysis.TicketRevision.Should().Be(5);
        analysis.InputHash.Should().Be("abc123");
        analysis.CreatedAt.Should().Be(now);
        analysis.Succeeded.Should().BeTrue();
        analysis.FailureReason.Should().BeNull();
        analysis.Suggestions.Should().BeEmpty();
    }

    [Fact]
    public void CreateFailed_ShouldStoreFailureReason()
    {
        var analysis = AIAnalysis.CreateFailed(
            Guid.NewGuid(), 1, "hash", DateTimeOffset.UtcNow, "Provider timeout");

        analysis.Succeeded.Should().BeFalse();
        analysis.FailureReason.Should().Be("Provider timeout");
    }

    [Fact]
    public void AddSuggestion_OnSucceeded_ShouldWork()
    {
        var analysis = AIAnalysis.CreateSucceeded(
            Guid.NewGuid(), 1, "hash", DateTimeOffset.UtcNow);
        var suggestion = AISuggestion.Create(
            analysis.Id, SuggestionType.Category, "NET", 0.85);

        analysis.AddSuggestion(suggestion);

        analysis.Suggestions.Should().HaveCount(1);
        analysis.Suggestions[0].SuggestedValue.Should().Be("NET");
    }

    [Fact]
    public void AddSuggestion_OnFailed_ShouldThrow()
    {
        var analysis = AIAnalysis.CreateFailed(
            Guid.NewGuid(), 1, "hash", DateTimeOffset.UtcNow, "timeout");

        var suggestion = AISuggestion.Create(
            analysis.Id, SuggestionType.Category, "NET", 0.85);

        var act = () => analysis.AddSuggestion(suggestion);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CreateSucceeded_NullInputHash_ShouldThrow()
    {
        var act = () => AIAnalysis.CreateSucceeded(
            Guid.NewGuid(), 1, null!, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentNullException>();
    }
}
