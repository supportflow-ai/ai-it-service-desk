using ServiceDesk.Domain.AIAssistance;
using Xunit;

namespace ServiceDesk.Domain.Tests.AIAssistance;

public sealed class InvariantRegressionTests
{
    private static AIAnalysis Analysis() => AIAnalysis.CreateSucceeded(Guid.NewGuid(), 0, new string('a', 64), DateTimeOffset.UtcNow);

    [Fact]
    public void SuggestionsCannotCrossParentsOrRepeatFields()
    {
        var first = Analysis();
        var second = Analysis();
        var suggestion = AISuggestion.Create(first.Id, SuggestionType.Category, "NET", .8);
        Assert.Throws<ArgumentException>(() => second.AddSuggestion(suggestion));
        first.AddSuggestion(suggestion);
        Assert.Throws<ArgumentException>(() => first.AddSuggestion(suggestion));
        Assert.Throws<ArgumentException>(() => first.AddSuggestion(AISuggestion.Create(first.Id, SuggestionType.Category, "VPN", null)));
    }

    [Theory]
    [InlineData(999, "NET")]
    [InlineData(0, "999")]
    [InlineData(1, "Critical")]
    [InlineData(2, "low")]
    public void UndefinedTypesAndValuesCannotReachPending(int type, string value) =>
        Assert.ThrowsAny<ArgumentException>(() => AISuggestion.Create(Guid.NewGuid(), (SuggestionType)type, value, null));

    [Fact]
    public void OverridePreservesFieldDomainAndPendingStateOnFailure()
    {
        var suggestion = AISuggestion.Create(Guid.NewGuid(), SuggestionType.Impact, "High", .9);
        Assert.Throws<ArgumentException>(() => suggestion.Override("NET", "actor", DateTimeOffset.UtcNow));
        Assert.Equal(SuggestionDecision.Pending, suggestion.Decision);
        Assert.Throws<ArgumentException>(() => AISuggestion.Create(Guid.Empty, SuggestionType.Category, "NET", null));
    }

    [Theory]
    [InlineData(-1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData(0, "hash")]
    [InlineData(0, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void SnapshotRequiresValidRevisionAndLowercaseSha256(long revision, string hash) =>
        Assert.ThrowsAny<ArgumentException>(() => AIAnalysis.CreateSucceeded(Guid.NewGuid(), revision, hash, DateTimeOffset.UtcNow));

    [Fact]
    public void SnapshotRequiresTicketIdentity() =>
        Assert.Throws<ArgumentException>(() => AIAnalysis.CreateSucceeded(Guid.Empty, 0, new string('a', 64), DateTimeOffset.UtcNow));

    [Fact]
    public void FieldBoundaryCannotBeForgedByDelimiterInjection()
    {
        Assert.NotEqual(InputHashComputer.Compute("x\nDESCRIPTION:y", "z"),
            InputHashComputer.Compute("x", "y\nDESCRIPTION:z"));
        Assert.NotEqual(InputHashComputer.Compute("é", "🙂"), InputHashComputer.Compute("é🙂", ""));
        Assert.Equal(InputHashComputer.Compute(" x ", null), InputHashComputer.Compute("x", ""));
        Assert.NotEqual(InputHashComputer.Compute("X", "y"), InputHashComputer.Compute("x", "y"));
    }

    [Fact]
    public void HashV2MatchesPublishedGoldenVector() =>
        Assert.Equal("e737e044b7ef976a6f2f2e97f1f2aaee633a2205cc16d051316bf2139d5dc02c",
            InputHashComputer.Compute("VPN issue", "Cannot connect"));
}
