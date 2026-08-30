using ServiceDesk.Domain.Common;
using FluentAssertions;
using Xunit;

namespace ServiceDesk.Domain.Tests.Common;

public class DomainDefaultsTests
{
    [Fact]
    public void NameMaxLength_ShouldBe256()
    {
        DomainDefaults.NameMaxLength.Should().Be(256);
    }

    [Fact]
    public void DescriptionMaxLength_ShouldBe4000()
    {
        DomainDefaults.DescriptionMaxLength.Should().Be(4000);
    }
}
