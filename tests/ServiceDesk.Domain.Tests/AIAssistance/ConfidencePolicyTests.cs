using ServiceDesk.Domain.AIAssistance;
using FluentAssertions;
using Xunit;

namespace ServiceDesk.Domain.Tests.AIAssistance;

public class ConfidencePolicyTests
{
    [Theory]
    [InlineData(0.0, true)]
    [InlineData(0.5, true)]
    [InlineData(1.0, true)]
    [InlineData(-0.01, false)]
    [InlineData(1.01, false)]
    public void IsValidScore_ShouldValidateRange(double score, bool expected)
    {
        ConfidencePolicy.IsValidScore(score).Should().Be(expected);
    }

    [Fact]
    public void IsValidScore_NaN_ShouldBeInvalid()
    {
        ConfidencePolicy.IsValidScore(double.NaN).Should().BeFalse();
    }

    [Fact]
    public void IsValidScore_Infinity_ShouldBeInvalid()
    {
        ConfidencePolicy.IsValidScore(double.PositiveInfinity).Should().BeFalse();
        ConfidencePolicy.IsValidScore(double.NegativeInfinity).Should().BeFalse();
    }

    [Fact]
    public void Classify_Null_ShouldReturnNull()
    {
        ConfidencePolicy.Classify(null).Should().BeNull();
    }

    [Fact]
    public void Classify_NaN_ShouldReturnNull()
    {
        ConfidencePolicy.Classify(double.NaN).Should().BeNull();
    }

    [Fact]
    public void Classify_Infinity_ShouldReturnNull()
    {
        ConfidencePolicy.Classify(double.PositiveInfinity).Should().BeNull();
    }

    [Fact]
    public void Classify_OutOfRange_ShouldReturnNull()
    {
        ConfidencePolicy.Classify(1.5).Should().BeNull();
        ConfidencePolicy.Classify(-0.1).Should().BeNull();
    }

    [Theory]
    [InlineData(0.0, ConfidenceBand.Low)]
    [InlineData(0.39, ConfidenceBand.Low)]
    [InlineData(0.4, ConfidenceBand.Medium)]
    [InlineData(0.5, ConfidenceBand.Medium)]
    [InlineData(0.74, ConfidenceBand.Medium)]
    [InlineData(0.75, ConfidenceBand.High)]
    [InlineData(1.0, ConfidenceBand.High)]
    public void Classify_ValidScore_ShouldReturnCorrectBand(double score, ConfidenceBand expected)
    {
        ConfidencePolicy.Classify(score).Should().Be(expected);
    }
}
