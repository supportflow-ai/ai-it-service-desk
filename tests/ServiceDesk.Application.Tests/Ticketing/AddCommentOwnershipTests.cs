using Moq;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.AddComment;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Ticketing;
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
        _writer.Setup(writer => writer.GetForUpdateAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _agentDirectory.Setup(directory => directory.GetDisplayNameAsync(_actorId, It.IsAny<CancellationToken>())).ReturnsAsync("Requester");

        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            useCase.ExecuteAsync(new(ticket.Id, "Attempt to comment on another ticket.")));
        _timelineStore.Verify(store => store.AddAsync(It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Requester_CanCommentOnOwnTicket()
    {
        var ticket = CreateTicket(_actorId);
        _user.SetupGet(user => user.Roles).Returns([RoleNames.Requester]);
        _writer.Setup(writer => writer.GetForUpdateAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _agentDirectory.Setup(directory => directory.GetDisplayNameAsync(_actorId, It.IsAny<CancellationToken>())).ReturnsAsync("Requester");
        _timelineStore.Setup(store => store.AddAsync(It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()))
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
        _writer.Setup(writer => writer.GetForUpdateAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _agentDirectory.Setup(directory => directory.GetDisplayNameAsync(_actorId, It.IsAny<CancellationToken>())).ReturnsAsync("Staff");
        _timelineStore.Setup(store => store.AddAsync(It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()))
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
