using Moq;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.CancelTicket;
using ServiceDesk.Application.Ticketing.ClaimTicket;
using ServiceDesk.Application.Ticketing.CloseTicket;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Application.Ticketing.ReopenTicket;
using ServiceDesk.Application.Ticketing.ResolveTicket;
using ServiceDesk.Application.Ticketing.UpdateTicket;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Ticketing;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public sealed class TicketLifecycleUseCaseTests
{
    private readonly Mock<ITicketWriter> _writer = new(MockBehavior.Strict);
    private readonly Mock<ITicketTimelineStore> _timeline = new();
    private readonly Mock<ICurrentUser> _user = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Guid _actor = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

    public TicketLifecycleUseCaseTests()
    {
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);
        _user.SetupGet(u => u.UserId).Returns(_actor.ToString());
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    [Fact]
    public async Task Claim_RequiresAgentRole_AndTransitionsToInProgress()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        var ticket = Ticket.Create(_actor, "Issue", "Problem description long enough", TicketCategories.VPN, _now.UtcDateTime);
        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(1);
        _writer.Setup(w => w.UpdateTicketAsync(ticket, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDto(ticket.Id, "IT-2026-0001", ticket.Title, ticket.Description, ticket.CategoryId, (int)TicketStatus.InProgress, ticket.CreatedAt));

        var useCase = new ClaimTicketUseCase(_writer.Object, _timeline.Object, _user.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new(ticket.Id, 1));

        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        _writer.VerifyAll();
    }

    [Fact]
    public async Task Claim_NonAgent_ThrowsUnauthorized()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        var useCase = new ClaimTicketUseCase(_writer.Object, _timeline.Object, _user.Object, _clock.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => useCase.ExecuteAsync(new(Guid.NewGuid())));
    }

    [Fact]
    public async Task Resolve_AgentRole_SetsResolved()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        var ticket = Ticket.Create(Guid.NewGuid(), "Issue", "Problem description long enough", TicketCategories.VPN, _now.UtcDateTime);
        ticket.Claim(_now.UtcDateTime);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(1);
        _writer.Setup(w => w.UpdateTicketAsync(ticket, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDto(ticket.Id, "IT-2026-0001", ticket.Title, ticket.Description, ticket.CategoryId, (int)TicketStatus.Resolved, ticket.CreatedAt));

        var useCase = new ResolveTicketUseCase(_writer.Object, _timeline.Object, _user.Object, _clock.Object);
        await useCase.ExecuteAsync(new(ticket.Id, 1));

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        _writer.VerifyAll();
    }

    [Fact]
    public async Task Reopen_OwnerRequester_SetsInProgress()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        var ticket = Ticket.Create(_actor, "Issue", "Problem description long enough", TicketCategories.VPN, _now.UtcDateTime);
        ticket.Claim(_now.UtcDateTime);
        ticket.Resolve(_now.UtcDateTime);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(1);
        _writer.Setup(w => w.UpdateTicketAsync(ticket, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDto(ticket.Id, "IT-2026-0001", ticket.Title, ticket.Description, ticket.CategoryId, (int)TicketStatus.InProgress, ticket.CreatedAt));

        var useCase = new ReopenTicketUseCase(_writer.Object, _timeline.Object, _user.Object, _clock.Object);
        await useCase.ExecuteAsync(new(ticket.Id, 1));

        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        _writer.VerifyAll();
    }

    [Fact]
    public async Task Close_OwnerRequester_SetsClosed()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        var ticket = Ticket.Create(_actor, "Issue", "Problem description long enough", TicketCategories.VPN, _now.UtcDateTime);
        ticket.Claim(_now.UtcDateTime);
        ticket.Resolve(_now.UtcDateTime);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(1);
        _writer.Setup(w => w.UpdateTicketAsync(ticket, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDto(ticket.Id, "IT-2026-0001", ticket.Title, ticket.Description, ticket.CategoryId, (int)TicketStatus.Closed, ticket.CreatedAt));

        var useCase = new CloseTicketUseCase(_writer.Object, _timeline.Object, _user.Object, _clock.Object);
        await useCase.ExecuteAsync(new(ticket.Id, 1));

        Assert.Equal(TicketStatus.Closed, ticket.Status);
        _writer.VerifyAll();
    }

    [Fact]
    public async Task Cancel_OwnerRequester_SetsCancelled()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        var ticket = Ticket.Create(_actor, "Issue", "Problem description long enough", TicketCategories.VPN, _now.UtcDateTime);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(1);
        _writer.Setup(w => w.UpdateTicketAsync(ticket, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDto(ticket.Id, "IT-2026-0001", ticket.Title, ticket.Description, ticket.CategoryId, (int)TicketStatus.Cancelled, ticket.CreatedAt));

        var useCase = new CancelTicketUseCase(_writer.Object, _timeline.Object, _user.Object, _clock.Object);
        await useCase.ExecuteAsync(new(ticket.Id, 1));

        Assert.Equal(TicketStatus.Cancelled, ticket.Status);
        _writer.VerifyAll();
    }

    [Fact]
    public async Task Update_OwnerRequester_UpdatesFields()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        var ticket = Ticket.Create(_actor, "Issue", "Problem description long enough", TicketCategories.VPN, _now.UtcDateTime);

        _writer.Setup(w => w.GetForUpdateAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _writer.Setup(w => w.UpdateTicketAsync(ticket, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDto(ticket.Id, "IT-2026-0001", "Updated Title", "Updated description at least 10", TicketCategories.HW, (int)TicketStatus.Submitted, ticket.CreatedAt));

        var useCase = new UpdateTicketUseCase(_writer.Object, _user.Object, _clock.Object);
        await useCase.ExecuteAsync(new(ticket.Id, "Updated Title", "Updated description at least 10", TicketCategories.HW));

        Assert.Equal("Updated Title", ticket.Title);
        Assert.Equal(TicketCategories.HW, ticket.CategoryId);
        _writer.VerifyAll();
    }
}
