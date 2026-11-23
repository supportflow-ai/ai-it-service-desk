using Moq;
using ServiceDesk.Modules.Ticketing.Application.AddComment;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public sealed class AddCommentSlaTests
{
    private readonly Mock<ITicketWriter> _writer = new(MockBehavior.Strict);
    private readonly Mock<ITicketTimelineStore> _timelineStore = new(MockBehavior.Strict);
    private readonly Mock<IAgentDirectory> _agentDirectory = new();
    private readonly Mock<ICurrentUser> _user = new();
    private readonly Mock<IClock> _clock = new();

    private readonly Guid _agentId = Guid.NewGuid();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

    public AddCommentSlaTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _agentDirectory.Setup(d => d.GetDisplayNameAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Test User");
        _timelineStore.Setup(s => s.FindRecentDuplicateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TimelineItemDto?)null);
    }

    [Fact]
    public async Task StaffComment_TriggersFirstResponse_AndSavesAtomically()
    {
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);
        _user.SetupGet(u => u.UserId).Returns(_agentId.ToString());
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var ticket = Ticket.Create(_requesterId, "Issue", "Problem description long enough", TicketCategories.VPN, _now.AddMinutes(-30).UtcDateTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), _now.AddMinutes(-30).UtcDateTime);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        _timelineStore.Setup(s => s.AddToOpenTicketAsync(
            It.Is<TicketComment>(c => !c.IsInternal && c.AuthorId == _agentId),
            It.Is<Ticket>(t => t.Id == ticket.Id && t.FirstRespondedAt == _now.UtcDateTime),
            null,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimelineItemDto(Guid.NewGuid(), ticket.Id, _agentId, "Test User", RoleNames.Agent, "Here is help", false, _now.UtcDateTime));

        var useCase = new AddCommentUseCase(_writer.Object, _timelineStore.Object, _agentDirectory.Object, _user.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new(ticket.Id, "Here is help"));

        Assert.NotNull(result);
        Assert.Equal(_now.UtcDateTime, ticket.FirstRespondedAt);
        Assert.False(ticket.FirstResponseBreached);
        _timelineStore.VerifyAll();
    }

    [Fact]
    public async Task OwnerComment_EvenWithAgentRole_DoesNotTriggerFirstResponse()
    {
        // Owner chính là Agent
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);
        _user.SetupGet(u => u.UserId).Returns(_requesterId.ToString());
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var ticket = Ticket.Create(_requesterId, "Issue", "Problem description long enough", TicketCategories.VPN, _now.AddMinutes(-30).UtcDateTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), _now.AddMinutes(-30).UtcDateTime);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        // Không modified ticket, gọi overload 2 tham số
        _timelineStore.Setup(s => s.AddToOpenTicketAsync(
            It.Is<TicketComment>(c => !c.IsInternal && c.AuthorId == _requesterId),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimelineItemDto(Guid.NewGuid(), ticket.Id, _requesterId, "Test User", RoleNames.Agent, "Owner extra info", false, _now.UtcDateTime));

        var useCase = new AddCommentUseCase(_writer.Object, _timelineStore.Object, _agentDirectory.Object, _user.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new(ticket.Id, "Owner extra info"));

        Assert.NotNull(result);
        Assert.Null(ticket.FirstRespondedAt); // Không được kích hoạt First Response!
        _timelineStore.VerifyAll();
    }

    [Fact]
    public async Task OwnerComment_InPendingUser_AutoResumesToInProgress_WithSystemAudit_DEC03()
    {
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);
        _user.SetupGet(u => u.UserId).Returns(_requesterId.ToString());
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _user.Setup(u => u.IsInRole(RoleNames.Requester)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var ticket = Ticket.Create(_requesterId, "Issue", "Problem description long enough", TicketCategories.VPN, _now.AddHours(-2).UtcDateTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), _now.AddHours(-2).UtcDateTime);
        ticket.Claim(_agentId, _now.AddHours(-1).UtcDateTime);
        ticket.MarkPending(TicketStatus.PendingUser, _now.AddMinutes(-30).UtcDateTime);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        _timelineStore.Setup(s => s.AddToOpenTicketAsync(
            It.Is<TicketComment>(c => !c.IsInternal && c.AuthorId == _requesterId && c.Content == "Provided OTP"),
            It.Is<Ticket>(t => t.Id == ticket.Id && t.Status == TicketStatus.InProgress && t.ResolutionSegmentStartedAt == _now.UtcDateTime),
            It.Is<TicketComment>(a => a.TicketId == ticket.Id && a.AuthorRole == "System" && a.Content.Contains("Tự động tiếp tục")),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimelineItemDto(Guid.NewGuid(), ticket.Id, _requesterId, "Test User", RoleNames.Requester, "Provided OTP", false, _now.UtcDateTime));

        var useCase = new AddCommentUseCase(_writer.Object, _timelineStore.Object, _agentDirectory.Object, _user.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new(ticket.Id, "Provided OTP"));

        Assert.NotNull(result);
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Equal(_now.UtcDateTime, ticket.ResolutionSegmentStartedAt);
        _timelineStore.VerifyAll();
    }

    [Fact]
    public async Task OwnerComment_InPendingExternal_DoesNotAutoResume()
    {
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);
        _user.SetupGet(u => u.UserId).Returns(_requesterId.ToString());
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _user.Setup(u => u.IsInRole(RoleNames.Requester)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var ticket = Ticket.Create(_requesterId, "Issue", "Problem description long enough", TicketCategories.VPN, _now.AddHours(-2).UtcDateTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), _now.AddHours(-2).UtcDateTime);
        ticket.Claim(_agentId, _now.AddHours(-1).UtcDateTime);
        ticket.MarkPending(TicketStatus.PendingExternal, _now.AddMinutes(-30).UtcDateTime);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        _timelineStore.Setup(s => s.AddToOpenTicketAsync(
            It.Is<TicketComment>(c => !c.IsInternal && c.AuthorId == _requesterId),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimelineItemDto(Guid.NewGuid(), ticket.Id, _requesterId, "Test User", RoleNames.Requester, "Any update?", false, _now.UtcDateTime));

        var useCase = new AddCommentUseCase(_writer.Object, _timelineStore.Object, _agentDirectory.Object, _user.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new(ticket.Id, "Any update?"));

        Assert.NotNull(result);
        Assert.Equal(TicketStatus.PendingExternal, ticket.Status); // Vẫn ở PendingExternal!
        Assert.Null(ticket.ResolutionSegmentStartedAt);
        _timelineStore.VerifyAll();
    }
}
