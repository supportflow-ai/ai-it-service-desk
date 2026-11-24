using Moq;
using ServiceDesk.Modules.Ticketing.Application.AddComment;
using ServiceDesk.Modules.Ticketing.Application.AddInternalNote;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public class CommentDedupTests
{
    private readonly Mock<ITicketWriter> _writer = new();
    private readonly Mock<ITicketTimelineStore> _timelineStore = new();
    private readonly Mock<IAgentDirectory> _agentDirectory = new();
    private readonly Mock<ICurrentUser> _user = new();
    private readonly Mock<IClock> _clock = new();

    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _ticketId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);

    public CommentDedupTests()
    {
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);
        _user.SetupGet(u => u.UserId).Returns(_actorId.ToString());
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _user.Setup(u => u.IsInRole(RoleNames.Requester)).Returns(true);
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    [Fact]
    public async Task AddComment_Within30Seconds_ReturnsExistingDuplicateWithoutWritingNew()
    {
        var ticket = Ticket.Create(_actorId, "Title", "Valid description here", TicketCategories.VPN, _now.AddHours(-1).UtcDateTime);
        _writer.Setup(w => w.GetForUpdateAsync(_ticketId, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var existingItem = new TimelineItemDto(
            Guid.NewGuid(),
            _ticketId,
            _actorId,
            "Requester",
            RoleNames.Requester,
            "Tin nhắn trùng lặp",
            false,
            _now.AddSeconds(-10).UtcDateTime);

        _timelineStore.Setup(s => s.FindRecentDuplicateAsync(_ticketId, _actorId, "Tin nhắn trùng lặp", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingItem);

        var useCase = new AddCommentUseCase(_writer.Object, _timelineStore.Object, _agentDirectory.Object, _user.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new(_ticketId, "Tin nhắn trùng lặp"));

        Assert.Equal(existingItem.Id, result.Id);
        _timelineStore.Verify(s => s.AddToOpenTicketAsync(It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddInternalNote_Within30Seconds_ReturnsExistingDuplicateWithoutWritingNew()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var ticket = Ticket.Create(Guid.NewGuid(), "Title", "Valid description here", TicketCategories.VPN, _now.AddHours(-1).UtcDateTime);
        _writer.Setup(w => w.GetForUpdateAsync(_ticketId, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var existingItem = new TimelineItemDto(
            Guid.NewGuid(),
            _ticketId,
            _actorId,
            "Agent Demo",
            RoleNames.Agent,
            "Ghi chú nội bộ trùng",
            true,
            _now.AddSeconds(-15).UtcDateTime);

        _timelineStore.Setup(s => s.FindRecentDuplicateAsync(_ticketId, _actorId, "Ghi chú nội bộ trùng", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingItem);

        var useCase = new AddInternalNoteUseCase(_writer.Object, _timelineStore.Object, _agentDirectory.Object, _user.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new(_ticketId, "Ghi chú nội bộ trùng"));

        Assert.Equal(existingItem.Id, result.Id);
        _timelineStore.Verify(s => s.AddAsync(It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
