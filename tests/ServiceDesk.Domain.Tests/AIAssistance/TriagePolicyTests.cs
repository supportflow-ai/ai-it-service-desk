using ServiceDesk.Domain.AIAssistance;
using ServiceDesk.Domain.Ticketing;
using FluentAssertions;
using Xunit;

namespace ServiceDesk.Domain.Tests.AIAssistance;

public class TriagePolicyTests
{
    [Fact]
    public void RequiresManualTriage_SEC_ShouldReturnTrue()
    {
        TriagePolicy.RequiresManualTriage(TicketCategory.SEC).Should().BeTrue();
    }

    [Theory]
    [InlineData(TicketCategory.ACC)]
    [InlineData(TicketCategory.NET)]
    [InlineData(TicketCategory.SW)]
    [InlineData(TicketCategory.OTHER)]
    public void RequiresManualTriage_NonSEC_ShouldReturnFalse(TicketCategory category)
    {
        TriagePolicy.RequiresManualTriage(category).Should().BeFalse();
    }

    [Theory]
    [InlineData("ACC", true)]
    [InlineData("NET", true)]
    [InlineData("VPN", true)]
    [InlineData("MAIL", true)]
    [InlineData("SW", true)]
    [InlineData("HW", true)]
    [InlineData("PRINT", true)]
    [InlineData("ACCESS", true)]
    [InlineData("EQUIP", true)]
    [InlineData("SEC", true)]
    [InlineData("OTHER", true)]
    [InlineData("acc", true)]   // case-insensitive
    [InlineData("UNKNOWN", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("DESKTOP_SUPPORT", false)]
    public void IsKnownCategory_ShouldValidateAgainstTaxonomy(string? code, bool expected)
    {
        TriagePolicy.IsKnownCategory(code).Should().Be(expected);
    }

    [Fact]
    public void ParseCategory_ValidCode_ShouldReturnEnum()
    {
        TriagePolicy.ParseCategory("NET").Should().Be(TicketCategory.NET);
    }

    [Fact]
    public void ParseCategory_UnknownCode_ShouldReturnNull()
    {
        TriagePolicy.ParseCategory("UNKNOWN").Should().BeNull();
    }

    [Fact]
    public void ParseImpact_ValidValue_ShouldReturnEnum()
    {
        TriagePolicy.ParseImpact("High").Should().Be(Impact.High);
    }

    [Fact]
    public void ParseImpact_InvalidValue_ShouldReturnNull()
    {
        TriagePolicy.ParseImpact("Critical").Should().BeNull();
    }

    [Fact]
    public void ParseUrgency_ValidValue_ShouldReturnEnum()
    {
        TriagePolicy.ParseUrgency("medium").Should().Be(Urgency.Medium);
    }

    [Fact]
    public void ParseUrgency_InvalidValue_ShouldReturnNull()
    {
        TriagePolicy.ParseUrgency("ASAP").Should().BeNull();
    }
}
