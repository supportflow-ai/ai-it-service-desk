using System;
using FluentAssertions;
using ServiceDesk.Modules.Ticketing.Domain;
using Xunit;

namespace ServiceDesk.Domain.Tests.Ticketing;

public class PrioritySlaTargetsTests
{
    [Theory]
    [InlineData(Priority.P1, 60, 240)]
    [InlineData(Priority.P2, 120, 480)]
    [InlineData(Priority.P3, 240, 1440)]
    [InlineData(Priority.P4, 480, 4320)]
    public void GetFor_ShouldReturn_ExpectedTargets_ForValidPriorities(
        Priority priority, int expectedFrMinutes, int expectedResMinutes)
    {
        var target = PrioritySlaTargets.GetFor(priority);

        target.FirstResponseMinutes.Should().Be(expectedFrMinutes);
        target.ResolutionMinutes.Should().Be(expectedResMinutes);
    }

    [Fact]
    public void GetFor_InvalidPriority_ShouldThrow()
    {
        var act = () => PrioritySlaTargets.GetFor((Priority)999);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
