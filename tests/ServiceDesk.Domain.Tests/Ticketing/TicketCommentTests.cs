using FluentAssertions;
using ServiceDesk.Domain.Ticketing;
using Xunit;

namespace ServiceDesk.Domain.Tests.Ticketing;

public class TicketCommentTests
{
    [Fact]
    public void Create_ValidInput_StoresValues()
    {
        var createdAt = DateTimeOffset.UtcNow;

        var comment = TicketComment.Create(Guid.NewGuid(), "user-1", "Message", createdAt);

        comment.AuthorId.Should().Be("user-1");
        comment.Content.Should().Be("Message");
        comment.CreatedAt.Should().Be(createdAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyContent_Throws(string content)
    {
        var action = () => TicketComment.Create(Guid.NewGuid(), "user-1", content, DateTimeOffset.UtcNow);

        action.Should().Throw<ArgumentException>();
    }
}
