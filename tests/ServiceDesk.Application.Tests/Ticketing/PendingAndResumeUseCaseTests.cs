using Moq;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Common;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.PendingTicket;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Application.ResumeTicket;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public sealed class PendingAndResumeUseCaseTests
{
    private readonly Mock<ITicketWriter> _writer = new(MockBehavior.Strict);
    private readonly Mock<ISupportTeamMembershipReader> _membershipReader = new();
    private readonly Mock<ICurrentUser> _user = new();
    private readonly Mock<IClock> _clock = new();

    private readonly Guid _actor = Guid.NewGuid();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly Guid _teamId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

    public PendingAndResumeUseCaseTests()
    {
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);
        _user.SetupGet(u => u.UserId).Returns(_actor.ToString());
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    private Ticket CreateInProgressTicket(Guid? assignedTo = null, Guid? teamId = null)
    {
        var ticket = Ticket.Create(_requesterId, "Issue", "Problem description long enough", TicketCategories.VPN, _now.AddHours(-1).UtcDateTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), _now.AddHours(-1).UtcDateTime);
        ticket.Claim(assignedTo ?? _actor, _now.AddMinutes(-30).UtcDateTime);
        if (teamId.HasValue)
        {
            // gán SupportTeamId qua reflection nếu private
            typeof(Ticket).GetProperty(nameof(Ticket.SupportTeamId))!.SetValue(ticket, teamId.Value);
        }
        return ticket;
    }

    [Fact]
    public async Task Pending_AsAssignedAgent_TransitionsToPendingUser_AndPausesResolution()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var ticket = CreateInProgressTicket(assignedTo: _actor, teamId: _teamId);
        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(1);
        _writer.Setup(w => w.UpdateTicketWithAuditAsync(ticket, It.Is<TicketComment>(a =>
            a.TicketId == ticket.Id &&
            a.AuthorId == _actor &&
            a.Content.Contains("chờ người dùng phản hồi")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDto(ticket.Id, "IT-2026-0001", ticket.Title, ticket.Description, ticket.CategoryId, (int)TicketStatus.PendingUser, ticket.CreatedAt));

        var useCase = new PendingTicketUseCase(_writer.Object, _membershipReader.Object, _user.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new(ticket.Id, TicketStatus.PendingUser, 1));

        Assert.Equal(TicketStatus.PendingUser, ticket.Status);
        Assert.Null(ticket.ResolutionSegmentStartedAt);
        Assert.True(ticket.ResolutionAccumulatedDuration > TimeSpan.Zero);
        _writer.VerifyAll();
    }

    [Fact]
    public async Task Pending_AsTeamLead_TransitionsToPendingExternal()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var otherAgent = Guid.NewGuid();
        var ticket = CreateInProgressTicket(assignedTo: otherAgent, teamId: _teamId);

        _membershipReader.Setup(m => m.IsLeadAsync(_teamId, _actor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(1);
        _writer.Setup(w => w.UpdateTicketWithAuditAsync(ticket, It.Is<TicketComment>(a =>
            a.TicketId == ticket.Id &&
            a.AuthorId == _actor &&
            a.Content.Contains("chờ đối tác bên ngoài")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDto(ticket.Id, "IT-2026-0001", ticket.Title, ticket.Description, ticket.CategoryId, (int)TicketStatus.PendingExternal, ticket.CreatedAt));

        var useCase = new PendingTicketUseCase(_writer.Object, _membershipReader.Object, _user.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new(ticket.Id, TicketStatus.PendingExternal, 1));

        Assert.Equal(TicketStatus.PendingExternal, ticket.Status);
        Assert.Null(ticket.ResolutionSegmentStartedAt);
        _writer.VerifyAll();
    }

    [Fact]
    public async Task Pending_AsAdmin_AllowedEvenWhenNotAssignedAndNotLead()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Admin]);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);

        var otherAgent = Guid.NewGuid();
        var ticket = CreateInProgressTicket(assignedTo: otherAgent, teamId: _teamId);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(1);
        _writer.Setup(w => w.UpdateTicketWithAuditAsync(ticket, It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDto(ticket.Id, "IT-2026-0001", ticket.Title, ticket.Description, ticket.CategoryId, (int)TicketStatus.PendingUser, ticket.CreatedAt));

        var useCase = new PendingTicketUseCase(_writer.Object, _membershipReader.Object, _user.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new(ticket.Id, TicketStatus.PendingUser, 1));

        Assert.Equal(TicketStatus.PendingUser, ticket.Status);
        _writer.VerifyAll();
    }

    [Fact]
    public async Task Pending_AsNonLeadNonAssigneeMember_ThrowsUnauthorizedAccessException()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var otherAgent = Guid.NewGuid();
        var ticket = CreateInProgressTicket(assignedTo: otherAgent, teamId: _teamId);

        _membershipReader.Setup(m => m.IsLeadAsync(_teamId, _actor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var useCase = new PendingTicketUseCase(_writer.Object, _membershipReader.Object, _user.Object, _clock.Object);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            useCase.ExecuteAsync(new(ticket.Id, TicketStatus.PendingUser, 1)));
    }

    [Fact]
    public async Task Pending_AsRequester_ThrowsUnauthorizedAccessException()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _user.Setup(u => u.IsInRole(RoleNames.Requester)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var useCase = new PendingTicketUseCase(_writer.Object, _membershipReader.Object, _user.Object, _clock.Object);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            useCase.ExecuteAsync(new(Guid.NewGuid(), TicketStatus.PendingUser, 1)));
    }

    [Fact]
    public async Task Pending_WithStaleRevision_ThrowsConcurrencyConflictException()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var ticket = CreateInProgressTicket(assignedTo: _actor, teamId: _teamId);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(2); // revision hiện tại là 2

        var useCase = new PendingTicketUseCase(_writer.Object, _membershipReader.Object, _user.Object, _clock.Object);
        // client truyền expectedRevision = 1 -> conflict
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            useCase.ExecuteAsync(new(ticket.Id, TicketStatus.PendingUser, 1)));
    }

    [Fact]
    public async Task Resume_AsAssignedAgent_TransitionsFromPendingUserToInProgress()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var ticket = CreateInProgressTicket(assignedTo: _actor, teamId: _teamId);
        ticket.MarkPending(TicketStatus.PendingUser, _now.AddMinutes(-10).UtcDateTime);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(1);
        _writer.Setup(w => w.UpdateTicketWithAuditAsync(ticket, It.Is<TicketComment>(a =>
            a.TicketId == ticket.Id &&
            a.AuthorId == _actor &&
            a.Content.Contains("tiếp tục xử lý")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDto(ticket.Id, "IT-2026-0001", ticket.Title, ticket.Description, ticket.CategoryId, (int)TicketStatus.InProgress, ticket.CreatedAt));

        var useCase = new ResumeTicketUseCase(_writer.Object, _membershipReader.Object, _user.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new(ticket.Id, 1));

        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Equal(_now.UtcDateTime, ticket.ResolutionSegmentStartedAt);
        _writer.VerifyAll();
    }

    [Fact]
    public async Task Resume_WhenTicketNotInPending_ThrowsArgumentException()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var ticket = CreateInProgressTicket(assignedTo: _actor, teamId: _teamId); // đang ở InProgress

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(1);

        var useCase = new ResumeTicketUseCase(_writer.Object, _membershipReader.Object, _user.Object, _clock.Object);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(new(ticket.Id, 1)));
    }
}
