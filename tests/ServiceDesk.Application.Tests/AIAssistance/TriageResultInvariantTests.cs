using ServiceDesk.Application.AIAssistance.Contracts;
using ServiceDesk.Domain.AIAssistance;
using ServiceDesk.Domain.Ticketing;
using Xunit;

namespace ServiceDesk.Application.Tests.AIAssistance;

public sealed class TriageResultInvariantTests
{
    [Theory]
    [InlineData(-.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void SuccessRejectsInvalidScores(double score) =>
        Assert.Throws<ArgumentException>(() => TriageResult.Success(TicketCategory.NET, null, null, score));

    [Fact]
    public void SuccessRejectsUndefinedEnumsAndOrphanScores()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TriageResult.Success((TicketCategory)999, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => TriageResult.Success(null, (Impact)999, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => TriageResult.Success(null, null, (Urgency)999));
        Assert.Throws<ArgumentException>(() => TriageResult.Success(null, null, null, .8));
        Assert.Throws<ArgumentException>(() => TriageResult.Success(null, null, null, impactScore: .8));
        Assert.Throws<ArgumentException>(() => TriageResult.Success(null, null, null, urgencyScore: .8));
    }

    [Fact]
    public void FailureCopiesAndProtectsValidationErrors()
    {
        var errors = new List<string> { "original" };
        var result = TriageResult.InvalidResponse("ai_response_invalid", errors);
        errors[0] = "changed";
        Assert.Equal("original", Assert.Single(result.ValidationErrors));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)result.ValidationErrors)[0] = "mutated");
        Assert.Null(result.SuggestedCategory);
        Assert.Null(result.CategoryConfidence);
        Assert.Equal(TriageOutcome.InvalidResponse, result.Outcome);
    }
}
