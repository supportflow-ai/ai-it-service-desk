using Moq;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Ticketing.Application.AssignTicket;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Application.UnassignTicket;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Ticketing.Domain;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public sealed class AssignmentUseCaseTests
{
    private readonly Mock<ITicketWriter> _writer = new(MockBehavior.Strict);
    private readonly Mock<IAgentDirectory> _agentDirectory = new();
    private readonly Mock<ISupportTeamMembershipReader> _membershipReader = new();
    private readonly Mock<ICurrentUser> _user = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _targetAgentId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

    public AssignmentUseCaseTests()
    {
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);
        _user.SetupGet(u => u.UserId).Returns(_actorId.ToString());
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _agentDirectory.Setup(g => g.IsAgentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    [Fact]
    public async Task Assign_ActiveLeadOfOwningTeam_Succeeds()
    {
        SetupAgentUser();
        var ticket = Ticket.Create(Guid.NewGuid(), "Title", "Valid description here", TicketCategories.VPN, _now.UtcDateTime);
        _membershipReader.Setup(m => m.IsLeadAsync(ticket.SupportTeamId!.Value, _actorId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _membershipReader.Setup(m => m.IsMemberAsync(ticket.SupportTeamId!.Value, _targetAgentId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(1);
        _writer.Setup(w => w.UpdateTicketWithAuditAsync(
                ticket,
                It.Is<TicketComment>(audit => audit.TicketId == ticket.Id &&
                    audit.AuthorId == _actorId &&
                    audit.Content == "Yêu cầu đã được phân công cho kỹ thuật viên."),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDto(ticket.Id, "IT-2026-0001", ticket.Title, ticket.Description, ticket.CategoryId, (int)TicketStatus.Assigned, ticket.CreatedAt));

        var useCase = new AssignTicketUseCase(_writer.Object, _agentDirectory.Object, _membershipReader.Object, _user.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new(ticket.Id, _targetAgentId, 1));

        Assert.Equal(TicketStatus.Assigned, ticket.Status);
        Assert.Equal(_targetAgentId, ticket.AssignedToId);
        _writer.VerifyAll();
    }

    [Fact]
    public async Task Assign_Admin_Succeeds()
    {
        SetupAdminUser();
        var ticket = Ticket.Create(Guid.NewGuid(), "Title", "Valid description here", TicketCategories.VPN, _now.UtcDateTime);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(1);
        _writer.Setup(w => w.UpdateTicketWithAuditAsync(
                ticket,
                It.Is<TicketComment>(audit => audit.TicketId == ticket.Id &&
                    audit.AuthorId == _actorId &&
                    audit.Content == "Yêu cầu đã được phân công cho kỹ thuật viên."),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDto(ticket.Id, "IT-2026-0001", ticket.Title, ticket.Description, ticket.CategoryId, (int)TicketStatus.Assigned, ticket.CreatedAt));

        var useCase = new AssignTicketUseCase(_writer.Object, _agentDirectory.Object, _membershipReader.Object, _user.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new(ticket.Id, _targetAgentId, 1));

        Assert.Equal(TicketStatus.Assigned, ticket.Status);
        Assert.Equal(_targetAgentId, ticket.AssignedToId);
        _writer.VerifyAll();
    }

    [Fact]
    public async Task Assign_Admin_TargetOutsideOwningTeam_SucceedsWithoutMembershipMutation()
    {
        SetupAdminUser();
        var ticket = Ticket.Create(Guid.NewGuid(), "Title", "Valid description here", TicketCategories.VPN, _now.UtcDateTime);
        var owningTeamId = ticket.SupportTeamId;
        var outsideAgentId = Guid.NewGuid();

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(1);
        _writer.Setup(w => w.UpdateTicketWithAuditAsync(ticket, It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDto(ticket.Id, "IT-2026-0001", ticket.Title, ticket.Description, ticket.CategoryId, (int)TicketStatus.Assigned, ticket.CreatedAt));

        var useCase = new AssignTicketUseCase(_writer.Object, _agentDirectory.Object, _membershipReader.Object, _user.Object, _clock.Object);
        await useCase.ExecuteAsync(new(ticket.Id, outsideAgentId, 1));

        Assert.Equal(outsideAgentId, ticket.AssignedToId);
        Assert.Equal(owningTeamId, ticket.SupportTeamId); // Team không đổi
    }

    [Fact]
    public async Task Assign_RegularAgentInTeam_IsForbidden()
    {
        SetupAgentUser();
        var ticket = Ticket.Create(Guid.NewGuid(), "Title", "Valid description here", TicketCategories.VPN, _now.UtcDateTime);
        _membershipReader.Setup(m => m.IsLeadAsync(ticket.SupportTeamId!.Value, _actorId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var useCase = new AssignTicketUseCase(_writer.Object, _agentDirectory.Object, _membershipReader.Object, _user.Object, _clock.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            useCase.ExecuteAsync(new(ticket.Id, _targetAgentId, 1)));
    }

    [Fact]
    public async Task Assign_DirectAssigneeWithoutLead_IsForbidden()
    {
        SetupAgentUser();
        var ticket = Ticket.Create(Guid.NewGuid(), "Title", "Valid description here", TicketCategories.VPN, _now.UtcDateTime);
        ticket.Assign(_actorId, _now.UtcDateTime); // Actor là direct assignee nhưng không phải Lead
        _membershipReader.Setup(m => m.IsLeadAsync(ticket.SupportTeamId!.Value, _actorId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var useCase = new AssignTicketUseCase(_writer.Object, _agentDirectory.Object, _membershipReader.Object, _user.Object, _clock.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            useCase.ExecuteAsync(new(ticket.Id, _targetAgentId, 1)));
    }

    [Fact]
    public async Task Assign_LeadOfOtherTeam_IsForbidden()
    {
        SetupAgentUser();
        var ticket = Ticket.Create(Guid.NewGuid(), "Title", "Valid description here", TicketCategories.VPN, _now.UtcDateTime);
        _membershipReader.Setup(m => m.IsLeadAsync(ticket.SupportTeamId!.Value, _actorId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var useCase = new AssignTicketUseCase(_writer.Object, _agentDirectory.Object, _membershipReader.Object, _user.Object, _clock.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            useCase.ExecuteAsync(new(ticket.Id, _targetAgentId, 1)));
    }

    [Fact]
    public async Task Assign_Lead_TargetOutsideOwningTeam_IsRejected()
    {
        SetupAgentUser();
        var ticket = Ticket.Create(Guid.NewGuid(), "Title", "Valid description here", TicketCategories.VPN, _now.UtcDateTime);
        _membershipReader.Setup(m => m.IsLeadAsync(ticket.SupportTeamId!.Value, _actorId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _membershipReader.Setup(m => m.IsMemberAsync(ticket.SupportTeamId!.Value, _targetAgentId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var useCase = new AssignTicketUseCase(_writer.Object, _agentDirectory.Object, _membershipReader.Object, _user.Object, _clock.Object);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(new(ticket.Id, _targetAgentId, 1)));

        Assert.Contains("thuộc nhóm hỗ trợ", ex.Message);
        Assert.Null(ticket.AssignedToId);
    }

    [Fact]
    public async Task Unassign_ActiveLeadOfOwningTeam_Succeeds()
    {
        SetupAgentUser();
        var ticket = Ticket.Create(Guid.NewGuid(), "Title", "Valid description here", TicketCategories.VPN, _now.UtcDateTime);
        ticket.Assign(_targetAgentId, _now.UtcDateTime);

        _membershipReader.Setup(m => m.IsLeadAsync(ticket.SupportTeamId!.Value, _actorId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(1);
        _writer.Setup(w => w.UpdateTicketWithAuditAsync(
                ticket,
                It.Is<TicketComment>(audit => audit.TicketId == ticket.Id &&
                    audit.AuthorId == _actorId &&
                    audit.Content == "Yêu cầu đã được hủy phân công xử lý."),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDto(ticket.Id, "IT-2026-0001", ticket.Title, ticket.Description, ticket.CategoryId, (int)TicketStatus.Submitted, ticket.CreatedAt));

        var useCase = new UnassignTicketUseCase(_writer.Object, _membershipReader.Object, _user.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new(ticket.Id, 1));

        Assert.Equal(TicketStatus.Submitted, ticket.Status);
        Assert.Null(ticket.AssignedToId);
        _writer.VerifyAll();
    }

    [Fact]
    public async Task Unassign_Admin_Succeeds()
    {
        SetupAdminUser();
        var ticket = Ticket.Create(Guid.NewGuid(), "Title", "Valid description here", TicketCategories.VPN, _now.UtcDateTime);
        ticket.Assign(_targetAgentId, _now.UtcDateTime);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(1);
        _writer.Setup(w => w.UpdateTicketWithAuditAsync(
                ticket,
                It.Is<TicketComment>(audit => audit.TicketId == ticket.Id &&
                    audit.AuthorId == _actorId &&
                    audit.Content == "Yêu cầu đã được hủy phân công xử lý."),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDto(ticket.Id, "IT-2026-0001", ticket.Title, ticket.Description, ticket.CategoryId, (int)TicketStatus.Submitted, ticket.CreatedAt));

        var useCase = new UnassignTicketUseCase(_writer.Object, _membershipReader.Object, _user.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new(ticket.Id, 1));

        Assert.Equal(TicketStatus.Submitted, ticket.Status);
        Assert.Null(ticket.AssignedToId);
        _writer.VerifyAll();
    }

    [Fact]
    public async Task Unassign_RegularAgent_IsForbidden()
    {
        SetupAgentUser();
        var ticket = Ticket.Create(Guid.NewGuid(), "Title", "Valid description here", TicketCategories.VPN, _now.UtcDateTime);
        ticket.Assign(_targetAgentId, _now.UtcDateTime);

        _membershipReader.Setup(m => m.IsLeadAsync(ticket.SupportTeamId!.Value, _actorId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var useCase = new UnassignTicketUseCase(_writer.Object, _membershipReader.Object, _user.Object, _clock.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            useCase.ExecuteAsync(new(ticket.Id, 1)));
    }

    [Fact]
    public async Task Assign_PersistsTicketAndAuditThroughWriterTogether()
    {
        SetupAgentUser();
        var ticket = Ticket.Create(Guid.NewGuid(), "Title", "Valid description here", TicketCategories.VPN, _now.UtcDateTime);
        _membershipReader.Setup(m => m.IsLeadAsync(ticket.SupportTeamId!.Value, _actorId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _membershipReader.Setup(m => m.IsMemberAsync(ticket.SupportTeamId!.Value, _targetAgentId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(1);
        _writer.Setup(w => w.UpdateTicketWithAuditAsync(ticket, It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDto(ticket.Id, "IT-2026-0001", ticket.Title, ticket.Description, ticket.CategoryId, (int)TicketStatus.Assigned, ticket.CreatedAt));

        var useCase = new AssignTicketUseCase(_writer.Object, _agentDirectory.Object, _membershipReader.Object, _user.Object, _clock.Object);
        await useCase.ExecuteAsync(new(ticket.Id, _targetAgentId, 1));

        _writer.Verify(w => w.UpdateTicketWithAuditAsync(
            ticket,
            It.Is<TicketComment>(audit => audit.TicketId == ticket.Id && audit.AuthorId == _actorId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Unassign_PersistsTicketAndAuditThroughWriterTogether()
    {
        SetupAgentUser();
        var ticket = Ticket.Create(Guid.NewGuid(), "Title", "Valid description here", TicketCategories.VPN, _now.UtcDateTime);
        ticket.Assign(_targetAgentId, _now.UtcDateTime);

        _membershipReader.Setup(m => m.IsLeadAsync(ticket.SupportTeamId!.Value, _actorId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(1);
        _writer.Setup(w => w.UpdateTicketWithAuditAsync(ticket, It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDto(ticket.Id, "IT-2026-0001", ticket.Title, ticket.Description, ticket.CategoryId, (int)TicketStatus.Submitted, ticket.CreatedAt));

        var useCase = new UnassignTicketUseCase(_writer.Object, _membershipReader.Object, _user.Object, _clock.Object);
        await useCase.ExecuteAsync(new(ticket.Id, 1));

        _writer.Verify(w => w.UpdateTicketWithAuditAsync(
            ticket,
            It.Is<TicketComment>(audit => audit.TicketId == ticket.Id && audit.AuthorId == _actorId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Assign_AsRequester_ThrowsUnauthorized()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var useCase = new AssignTicketUseCase(_writer.Object, _agentDirectory.Object, _membershipReader.Object, _user.Object, _clock.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            useCase.ExecuteAsync(new(Guid.NewGuid(), _targetAgentId)));
    }

    private void SetupAgentUser()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
    }

    private void SetupAdminUser()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Admin]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(true);
    }
}
