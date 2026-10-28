using Moq;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.AddComment;
using ServiceDesk.Application.Ticketing.AddInternalNote;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.GetTicketTimeline;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Ticketing;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public sealed class TimelineSecurityTests
{
    private readonly Mock<ITicketQueries> _ticketQueries = new();
    private readonly Mock<ITicketWriter> _ticketWriter = new();
    private readonly Mock<ITicketTimelineStore> _timelineStore = new();
    private readonly Mock<ICurrentUser> _user = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Guid _ticketId = Guid.NewGuid();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly Guid _agentId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

    public TimelineSecurityTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    [Fact]
    public async Task TC307_Requester_NeverSeesInternalNotes_ServerSideFilter()
    {
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);
        _user.SetupGet(u => u.UserId).Returns(_requesterId.ToString());
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var ticketDto = new TicketDto(_ticketId, "IT-2026-0001", "VPN", "Issue", "VPN", 1, _now.UtcDateTime);
        _ticketQueries.Setup(q => q.GetByIdAsync(_ticketId, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticketDto);

        var publicComment = new TimelineItemDto(Guid.NewGuid(), _ticketId, _requesterId, "User", "Requester", "Hello", false, _now.UtcDateTime);
        _timelineStore.Setup(s => s.GetForTicketAsync(_ticketId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([publicComment]);

        var useCase = new GetTicketTimelineUseCase(_ticketQueries.Object, _timelineStore.Object, _user.Object);
        var result = await useCase.ExecuteAsync(new(_ticketId));

        Assert.Single(result);
        Assert.False(result[0].IsInternal);
        _timelineStore.Verify(s => s.GetForTicketAsync(_ticketId, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TC307_Requester_CreatingInternalNote_IsForbiddenWith403()
    {
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);
        _user.SetupGet(u => u.UserId).Returns(_requesterId.ToString());
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var useCase = new AddInternalNoteUseCase(_ticketWriter.Object, _timelineStore.Object, _user.Object, _clock.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            useCase.ExecuteAsync(new(_ticketId, "Leaked secret internal diagnostic")));
    }

    [Fact]
    public async Task Agent_CanSeeInternalNotes()
    {
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);
        _user.SetupGet(u => u.UserId).Returns(_agentId.ToString());
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var ticketDto = new TicketDto(_ticketId, "IT-2026-0001", "VPN", "Issue", "VPN", 1, _now.UtcDateTime);
        _ticketQueries.Setup(q => q.GetByIdAsync(_ticketId, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticketDto);

        var publicComment = new TimelineItemDto(Guid.NewGuid(), _ticketId, _requesterId, "User", "Requester", "Hello", false, _now.UtcDateTime);
        var internalNote = new TimelineItemDto(Guid.NewGuid(), _ticketId, _agentId, "Agent", "Agent", "Secret note", true, _now.UtcDateTime);
        _timelineStore.Setup(s => s.GetForTicketAsync(_ticketId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([publicComment, internalNote]);

        var useCase = new GetTicketTimelineUseCase(_ticketQueries.Object, _timelineStore.Object, _user.Object);
        var result = await useCase.ExecuteAsync(new(_ticketId));

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.IsInternal);
        _timelineStore.Verify(s => s.GetForTicketAsync(_ticketId, true, It.IsAny<CancellationToken>()), Times.Once);
    }
}
