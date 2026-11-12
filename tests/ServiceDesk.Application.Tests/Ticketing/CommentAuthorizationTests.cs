using Moq;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.AddComment;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Ticketing.Domain;
using Xunit;
using ServiceDesk.SharedKernel.Application;

namespace ServiceDesk.Application.Tests.Ticketing;

public sealed class CommentAuthorizationTests
{
    private readonly Mock<ITicketWriter> _writer = new(MockBehavior.Strict);
    private readonly Mock<ITicketTimelineStore> _timeline = new(MockBehavior.Strict);
    private readonly Mock<IAgentDirectory> _directory = new(MockBehavior.Strict);
    private readonly Mock<ICurrentUser> _user = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Guid _actor = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);

    public CommentAuthorizationTests()
    {
        _user.SetupGet(user => user.IsAuthenticated).Returns(true);
        _user.SetupGet(user => user.UserId).Returns(_actor.ToString());
        _clock.SetupGet(clock => clock.UtcNow).Returns(_now);
    }

    [Theory]
    [InlineData("Requester", true, "Requester")]
    [InlineData("Agent", false, "Agent")]
    [InlineData("Admin", false, "Admin")]
    [InlineData("Requester,Agent", false, "Agent")]
    [InlineData("Requester,Admin", false, "Admin")]
    [InlineData("Agent,Admin", false, "Admin")]
    [InlineData("Requester,Agent,Admin", false, "Admin")]
    public async Task AuthorizedActor_CanCommentWithinExistingScope(string roleList, bool ownsTicket, string expectedRole)
    {
        SetRoles(roleList);
        var ticket = CreateTicket(ownsTicket ? _actor : Guid.NewGuid());
        _writer.Setup(writer => writer.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _directory.Setup(directory => directory.GetDisplayNameAsync(_actor, It.IsAny<CancellationToken>())).ReturnsAsync("Người dùng kiểm thử");
        TicketComment? saved = null;
        _timeline.Setup(store => store.AddToOpenTicketAsync(It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()))
            .Callback<TicketComment, CancellationToken>((comment, _) => saved = comment)
            .ReturnsAsync((TicketComment comment, CancellationToken _) => new TimelineItemDto(
                comment.Id, comment.TicketId, comment.AuthorId, comment.AuthorName, comment.AuthorRole,
                comment.Content, comment.IsInternal, comment.CreatedAt));

        var result = await CreateUseCase().ExecuteAsync(new(ticket.Id, "Nội dung bình luận"));

        Assert.NotNull(saved);
        Assert.Equal(ticket.Id, saved.TicketId);
        Assert.Equal(_actor, saved.AuthorId);
        Assert.False(saved.IsInternal);
        Assert.Equal(_now.UtcDateTime, saved.CreatedAt);
        Assert.Equal(expectedRole, saved.AuthorRole);
        Assert.Equal(saved.Id, result.Id);
        _writer.VerifyAll();
        _directory.VerifyAll();
        _timeline.Verify(store => store.AddToOpenTicketAsync(It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OtherRequester_IsNotFound_WithoutAuthorLookupOrTimelineWrite()
    {
        SetRoles(RoleNames.Requester);
        var ticket = CreateTicket(Guid.NewGuid());
        _writer.Setup(writer => writer.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        await Assert.ThrowsAsync<NotFoundException>(() => CreateUseCase().ExecuteAsync(new(ticket.Id, "Không được ghi")));

        _directory.VerifyNoOtherCalls();
        _timeline.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MissingTicket_IsNotFound_WithoutAuthorLookupOrTimelineWrite()
    {
        SetRoles(RoleNames.Requester);
        var id = Guid.NewGuid();
        _writer.Setup(writer => writer.GetForUpdateAsync(id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>())).ReturnsAsync((Ticket?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => CreateUseCase().ExecuteAsync(new(id, "Không được ghi")));

        _directory.VerifyNoOtherCalls();
        _timeline.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, "Requester")]
    [InlineData(true, "UnknownRole")]
    public async Task InvalidActor_IsRejectedBeforePersistence(bool authenticated, string role)
    {
        SetRoles(role);
        _user.SetupGet(user => user.IsAuthenticated).Returns(authenticated);

        await Assert.ThrowsAsync<ForbiddenException>(() => CreateUseCase().ExecuteAsync(new(Guid.NewGuid(), "Không được ghi")));

        _writer.VerifyNoOtherCalls();
        _directory.VerifyNoOtherCalls();
        _timeline.VerifyNoOtherCalls();
    }

    private AddCommentUseCase CreateUseCase() => new(_writer.Object, _timeline.Object, _directory.Object, _user.Object, _clock.Object);

    private Ticket CreateTicket(Guid requester) => Ticket.Create(requester, "Lỗi VPN", "Mô tả lỗi đủ độ dài", TicketCategories.VPN, _now.UtcDateTime);

    private void SetRoles(string roleList)
    {
        var roles = roleList.Split(',');
        _user.SetupGet(user => user.Roles).Returns(roles);
        _user.Setup(user => user.IsInRole(It.IsAny<string>())).Returns((string role) => roles.Contains(role));
    }
}
