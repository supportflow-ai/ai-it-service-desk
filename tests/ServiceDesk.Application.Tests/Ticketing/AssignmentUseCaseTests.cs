using Moq;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Identity.Ports;
using ServiceDesk.Application.Ticketing.AssignTicket;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Application.Ticketing.UnassignTicket;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Ticketing;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public sealed class AssignmentUseCaseTests
{
    private readonly Mock<ITicketWriter> _writer = new(MockBehavior.Strict);
    private readonly Mock<ITicketTimelineStore> _timeline = new();
    private readonly Mock<IAgentDirectory> _agentDirectory = new();
    private readonly Mock<ICurrentUser> _user = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Guid _agentId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

    public AssignmentUseCaseTests()
    {
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);
        _user.SetupGet(u => u.UserId).Returns(_agentId.ToString());
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _agentDirectory.Setup(g => g.IsAgentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    [Fact]
    public async Task Assign_AsAgent_SetsAssignedStatus()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var ticket = Ticket.Create(Guid.NewGuid(), "Title", "Valid description here", TicketCategories.VPN, _now.UtcDateTime);
        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(1);
        _writer.Setup(w => w.UpdateTicketAsync(ticket, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDto(ticket.Id, "IT-2026-0001", ticket.Title, ticket.Description, ticket.CategoryId, (int)TicketStatus.Assigned, ticket.CreatedAt));

        var useCase = new AssignTicketUseCase(_writer.Object, _timeline.Object, _agentDirectory.Object, _user.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new(ticket.Id, _agentId, 1));

        Assert.Equal(TicketStatus.Assigned, ticket.Status);
        Assert.Equal(_agentId, ticket.AssignedToId);
        _writer.VerifyAll();
    }

    [Fact]
    public async Task Unassign_AsAgent_ResetsSubmittedStatus()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var ticket = Ticket.Create(Guid.NewGuid(), "Title", "Valid description here", TicketCategories.VPN, _now.UtcDateTime);
        ticket.Assign(_agentId, _now.UtcDateTime);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(1);
        _writer.Setup(w => w.UpdateTicketAsync(ticket, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDto(ticket.Id, "IT-2026-0001", ticket.Title, ticket.Description, ticket.CategoryId, (int)TicketStatus.Submitted, ticket.CreatedAt));

        var useCase = new UnassignTicketUseCase(_writer.Object, _timeline.Object, _user.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new(ticket.Id, 1));

        Assert.Equal(TicketStatus.Submitted, ticket.Status);
        Assert.Null(ticket.AssignedToId);
        _writer.VerifyAll();
    }

    [Fact]
    public async Task Assign_AsRequester_ThrowsUnauthorized()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var useCase = new AssignTicketUseCase(_writer.Object, _timeline.Object, _agentDirectory.Object, _user.Object, _clock.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            useCase.ExecuteAsync(new(Guid.NewGuid(), _agentId)));
    }
}
