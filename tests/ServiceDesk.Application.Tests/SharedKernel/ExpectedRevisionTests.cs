using ServiceDesk.SharedKernel.Application;
using Xunit;

namespace ServiceDesk.Application.Tests.SharedKernel;

/// <summary>Quy tắc <c>expectedRevision</c> dùng chung cho Ticketing và Knowledge.</summary>
public sealed class ExpectedRevisionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(0u)]
    public void MissingOrZeroIsInvalidInputAndNeverReadsTheCurrentRevision(uint? expected)
    {
        var reads = 0;

        var error = Assert.Throws<InvalidInputException>(() => ExpectedRevision.Require(expected, () => { reads++; return 5; }));

        Assert.Equal(ExpectedRevision.RequiredCode, error.Code);
        Assert.Equal(ExpectedRevision.RequiredMessage, error.Message);
        Assert.Equal(400, error.StatusCode);
        Assert.Equal(0, reads);
    }

    [Fact]
    public void MatchingRevisionPasses() => ExpectedRevision.Require(7, () => 7);

    [Fact]
    public void StaleRevisionIsConflictWithDefaultMessage()
    {
        var error = Assert.Throws<ConcurrencyConflictException>(() => ExpectedRevision.Require(6, () => 7));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal(ConcurrencyConflictException.ErrorCode, error.Code);
        Assert.Equal(new ConcurrencyConflictException().Message, error.Message);
    }

    [Fact]
    public void StaleRevisionUsesTheModulesOwnMessage()
    {
        var error = Assert.Throws<ConcurrencyConflictException>(() =>
            ExpectedRevision.Require(6, () => 7, "Phiên bản bài viết đã thay đổi. Vui lòng tải lại."));

        Assert.Equal("Phiên bản bài viết đã thay đổi. Vui lòng tải lại.", error.Message);
    }
}
