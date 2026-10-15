using ServiceDesk.Domain.AIAssistance;
using FluentAssertions;
using Xunit;

namespace ServiceDesk.Domain.Tests.AIAssistance;

public class InputHashComputerTests
{
    [Fact]
    public void Compute_SameInput_ShouldReturnSameHash()
    {
        var hash1 = InputHashComputer.Compute("Title A", "Description B");
        var hash2 = InputHashComputer.Compute("Title A", "Description B");

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void Compute_DifferentTitle_ShouldReturnDifferentHash()
    {
        var hash1 = InputHashComputer.Compute("Title A", "Description");
        var hash2 = InputHashComputer.Compute("Title B", "Description");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Compute_DifferentDescription_ShouldReturnDifferentHash()
    {
        var hash1 = InputHashComputer.Compute("Title", "Desc A");
        var hash2 = InputHashComputer.Compute("Title", "Desc B");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Compute_NullDescription_ShouldNotThrow()
    {
        var hash = InputHashComputer.Compute("Title", null);

        hash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Compute_TrimsWhitespace_ShouldReturnSameHash()
    {
        var hash1 = InputHashComputer.Compute("Title", "Desc");
        var hash2 = InputHashComputer.Compute("  Title  ", "  Desc  ");

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void Compute_NullTitle_ShouldThrow()
    {
        var act = () => InputHashComputer.Compute(null!, "Desc");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Compute_ShouldReturnHexString()
    {
        var hash = InputHashComputer.Compute("test", "desc");

        hash.Should().MatchRegex("^[0-9a-f]{64}$"); // SHA-256 = 64 hex chars
    }
}
