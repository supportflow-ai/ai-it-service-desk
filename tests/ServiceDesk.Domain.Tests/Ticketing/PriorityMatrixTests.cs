using ServiceDesk.Domain.Ticketing;
using FluentAssertions;
using Xunit;

namespace ServiceDesk.Domain.Tests.Ticketing;

public class PriorityMatrixTests
{
    [Theory]
    [InlineData(Impact.High, Urgency.High, Priority.P1)]
    [InlineData(Impact.High, Urgency.Medium, Priority.P2)]
    [InlineData(Impact.High, Urgency.Low, Priority.P3)]
    [InlineData(Impact.Medium, Urgency.High, Priority.P2)]
    [InlineData(Impact.Medium, Urgency.Medium, Priority.P3)]
    [InlineData(Impact.Medium, Urgency.Low, Priority.P4)]
    [InlineData(Impact.Low, Urgency.High, Priority.P3)]
    [InlineData(Impact.Low, Urgency.Medium, Priority.P4)]
    [InlineData(Impact.Low, Urgency.Low, Priority.P4)]
    public void Calculate_ShouldReturn_CorrectPriority_ForAll9Combinations(
        Impact impact, Urgency urgency, Priority expectedPriority)
    {
        var result = PriorityMatrix.Calculate(impact, urgency);

        result.Should().Be(expectedPriority);
    }

    [Fact]
    public void Calculate_InvalidImpact_ShouldThrow()
    {
        var act = () => PriorityMatrix.Calculate((Impact)99, Urgency.High);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Calculate_InvalidUrgency_ShouldThrow()
    {
        var act = () => PriorityMatrix.Calculate(Impact.High, (Urgency)99);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
