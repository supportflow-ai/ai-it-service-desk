using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

/// <summary>
/// <see cref="AgentQueueFilter.FromRaw"/> giữ đúng hành vi cũ của hàng đợi Agent: scope không nhận diện được
/// nghĩa là không lọc, priority được phân tích như <see cref="Enum.TryParse{TEnum}(string?, bool, out TEnum)"/>.
/// </summary>
public sealed class AgentQueueFilterTests
{
    [Theory]
    [InlineData("unassigned", AgentQueueScope.Unassigned)]
    [InlineData("UNASSIGNED", AgentQueueScope.Unassigned)]
    [InlineData("my", AgentQueueScope.Mine)]
    [InlineData("My", AgentQueueScope.Mine)]
    [InlineData(null, AgentQueueScope.All)]
    [InlineData("", AgentQueueScope.All)]
    [InlineData("all", AgentQueueScope.All)]
    [InlineData("mine", AgentQueueScope.All)]
    [InlineData(" my", AgentQueueScope.All)]
    [InlineData("unknown", AgentQueueScope.All)]
    public void ScopeIsCaseInsensitiveAndUnknownValuesMeanNoFilter(string? scope, AgentQueueScope expected) =>
        Assert.Equal(expected, AgentQueueFilter.FromRaw(null, null, scope, null, false).Scope);

    [Theory]
    [InlineData("P1", Priority.P1)]
    [InlineData("p2", Priority.P2)]
    [InlineData("P3", Priority.P3)]
    [InlineData(" P4 ", Priority.P4)]
    [InlineData("0", Priority.P1)]
    public void ValidPriorityIsParsedCaseInsensitively(string raw, Priority expected) =>
        Assert.Equal(expected, AgentQueueFilter.FromRaw(null, null, null, raw, false).Priority);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankPriorityMeansNoFilter(string? raw) =>
        Assert.Null(AgentQueueFilter.FromRaw(null, null, null, raw, false).Priority);

    [Theory]
    [InlineData("P5")]
    [InlineData("high")]
    [InlineData("P1P2")]
    public void UnreadablePriorityIsInvalidInput(string raw)
    {
        var error = Assert.Throws<InvalidInputException>(() => AgentQueueFilter.FromRaw(null, null, null, raw, false));

        Assert.Equal("priority_invalid", error.Code);
        Assert.Equal("priority phải là một trong P1, P2, P3, P4.", error.Message);
    }

    [Fact]
    public void OtherFieldsPassThroughUnchanged()
    {
        var filter = AgentQueueFilter.FromRaw(7, "NET", "my", "P2", true);

        Assert.Equal(new AgentQueueFilter(7, "NET", AgentQueueScope.Mine, Priority.P2, true), filter);
    }
}
