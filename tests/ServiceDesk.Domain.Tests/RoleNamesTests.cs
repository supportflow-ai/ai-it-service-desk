using FluentAssertions;
using ServiceDesk.Domain.Identity;
using Xunit;

namespace ServiceDesk.Domain.Tests;

public class RoleNamesTests
{
    [Fact]
    public void RoleNames_Contains_Admin_Agent_Requester()
    {
        RoleNames.All.Should().HaveCount(3);
        RoleNames.All.Should().Contain(RoleNames.Admin);
        RoleNames.All.Should().Contain(RoleNames.Agent);
        RoleNames.All.Should().Contain(RoleNames.Requester);
    }

    [Fact]
    public void PolicyNames_Are_Defined_Properly()
    {
        PolicyNames.RequireAdmin.Should().Be("RequireAdmin");
        PolicyNames.RequireAgent.Should().Be("RequireAgent");
        PolicyNames.RequireRequester.Should().Be("RequireRequester");
    }
}
