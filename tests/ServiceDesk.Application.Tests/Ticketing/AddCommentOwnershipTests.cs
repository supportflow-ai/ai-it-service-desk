using Moq;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.AddComment;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Ticketing.Domain;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public sealed class AddCommentOwnershipTests
{
    private readonly Mock<ITicketWriter> _writer = new();
    private readonly Mock<ITicketTimelineStore> _timelineStore = new();
    private readonly Mock<IAgentDirectory> _agentDirectory = new();
    private readonly Mock<ICurrentUser> _user = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

    public AddCommentOwnershipTests()
    {
        _user.SetupGet(user => user.IsAuthenticated).Returns(true);
        _user.SetupGet(user => user.UserId).Returns(_actorId.ToString());
        _clock.SetupGet(clock => clock.UtcNow).Returns(_now);
    }

    [Fact]
    public async Task Requester_CannotCommentOnAnotherRequestersTicket()
    {
        var ticket = CreateTicket(Guid.NewGuid());
        _user.SetupGet(user => user.Roles).Returns([RoleNames.Requester]);
        _user.Setup(user => user.IsInRole(RoleNames.Agent)).Returns(false);
        _user.Setup(user => user.IsInRole(RoleNames.Admin)).Returns(false);
        _writer.Setup(writer => writer.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _agentDirectory.Setup(directory => directory.GetDisplayNameAsync(_actorId, It.IsAny<CancellationToken>())).ReturnsAsync("Requester");

        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            useCase.ExecuteAsync(new(ticket.Id, "Attempt to comment on another ticket.")));
        _timelineStore.Verify(store => store.AddToOpenTicketAsync(It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task Requester_CannotCommentOnTerminalTicket(TicketStatus status)
    {
        var ticket = CreateTicket(_actorId);
        if (status == TicketStatus.Closed)
        {
            ticket.Claim(_actorId, _now.UtcDateTime);
            ticket.Resolve(_now.UtcDateTime);
            ticket.Close(_now.UtcDateTime);
        }
        else
        {
            ticket.Cancel(_now.UtcDateTime);
        }
        _user.SetupGet(user => user.Roles).Returns([RoleNames.Requester]);
        _writer.Setup(writer => writer.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        await Assert.ThrowsAsync<ArgumentException>(() => CreateUseCase().ExecuteAsync(new(ticket.Id, "Blocked comment")));

        _agentDirectory.VerifyNoOtherCalls();
        _timelineStore.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task Requester_GetsNotFoundForAnotherRequestersTerminalTicket(TicketStatus status)
    {
        var otherOwner = Guid.NewGuid();
        var ticket = CreateTicket(otherOwner);
        if (status == TicketStatus.Closed)
        {
            ticket.Claim(Guid.NewGuid(), _now.UtcDateTime);
            ticket.Resolve(_now.UtcDateTime);
            ticket.Close(_now.UtcDateTime);
        }
        else
        {
            ticket.Cancel(_now.UtcDateTime);
        }
        _user.SetupGet(user => user.Roles).Returns([RoleNames.Requester]);
        _writer.Setup(writer => writer.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        // Không lộ ticket tồn tại hay đã kết thúc: giống hệt ID không tồn tại.
        await Assert.ThrowsAsync<KeyNotFoundException>(() => CreateUseCase().ExecuteAsync(new(ticket.Id, "Probe")));
        _timelineStore.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Requester_CanCommentOnOwnTicket()
    {
        var ticket = CreateTicket(_actorId);
        _user.SetupGet(user => user.Roles).Returns([RoleNames.Requester]);
        _writer.Setup(writer => writer.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _agentDirectory.Setup(directory => directory.GetDisplayNameAsync(_actorId, It.IsAny<CancellationToken>())).ReturnsAsync("Requester");
        _timelineStore.Setup(store => store.AddToOpenTicketAsync(It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimelineItemDto(Guid.NewGuid(), ticket.Id, _actorId, "Requester", RoleNames.Requester, "Owner comment", false, _now.UtcDateTime));

        var result = await CreateUseCase().ExecuteAsync(new(ticket.Id, "Owner comment"));

        Assert.Equal(ticket.Id, result.TicketId);
        Assert.Equal(_actorId, result.AuthorId);
    }

    [Theory]
    [InlineData(RoleNames.Agent)]
    [InlineData(RoleNames.Admin)]
    public async Task Staff_CanCommentOnAnotherRequestersTicket(string role)
    {
        var ticket = CreateTicket(Guid.NewGuid());
        _user.SetupGet(user => user.Roles).Returns([role]);
        _user.Setup(user => user.IsInRole(RoleNames.Agent)).Returns(role == RoleNames.Agent);
        _user.Setup(user => user.IsInRole(RoleNames.Admin)).Returns(role == RoleNames.Admin);
        _writer.Setup(writer => writer.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _agentDirectory.Setup(directory => directory.GetDisplayNameAsync(_actorId, It.IsAny<CancellationToken>())).ReturnsAsync("Staff");
        _timelineStore.Setup(store => store.AddToOpenTicketAsync(It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimelineItemDto(Guid.NewGuid(), ticket.Id, _actorId, "Staff", role, "Staff comment", false, _now.UtcDateTime));

        var result = await CreateUseCase().ExecuteAsync(new(ticket.Id, "Staff comment"));

        Assert.Equal(ticket.Id, result.TicketId);
    }

    private AddCommentUseCase CreateUseCase() => new(
        _writer.Object,
        _timelineStore.Object,
        _agentDirectory.Object,
        _user.Object,
        _clock.Object);

    private Ticket CreateTicket(Guid requesterId) => Ticket.Create(
        requesterId,
        "Comment ownership",
        "Ticket used to verify comment ownership.",
        TicketCategories.NET,
        _now.UtcDateTime);
}
