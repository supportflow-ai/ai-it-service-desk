using Moq;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Application.TriageTicket;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public class TriageTicketUseCaseTests
{
    private readonly Mock<ITicketWriter> _writer = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IClock> _clock = new();

    private readonly Guid _agentId = Guid.NewGuid();
    private readonly Guid _ticketId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 11, 24, 8, 0, 0, TimeSpan.Zero);

    public TriageTicketUseCaseTests()
    {
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(_agentId.ToString());
        _currentUser.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    private Ticket CreateTestTicket()
    {
        return Ticket.Create(
            Guid.NewGuid(),
            "Title",
            "Valid description here",
            TicketCategories.VPN,
            _now.UtcDateTime);
    }

    [Fact]
    public async Task TriageTicket_AsAgent_SetsImpactUrgencyAndCalculatesPriority()
    {
        var ticket = CreateTestTicket();
        _writer.Setup(w => w.GetForUpdateAsync(_ticketId, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(1u);

        var expectedDto = new TicketDto(
            ticket.Id,
            "IT-2026-0001",
            ticket.Title,
            ticket.Description,
            ticket.CategoryId,
            (int)ticket.Status,
            ticket.CreatedAt,
            Priority: "P1");

        _writer.Setup(w => w.UpdateTicketWithAuditAsync(ticket, It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var useCase = new TriageTicketUseCase(_writer.Object, _currentUser.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new(_ticketId, Impact.High, Urgency.High, ExpectedRevision: 1u));

        Assert.Equal(Impact.High, ticket.Impact);
        Assert.Equal(Urgency.High, ticket.Urgency);
        Assert.Equal(Priority.P1, ticket.Priority);
        _writer.Verify(w => w.UpdateTicketWithAuditAsync(ticket, It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TriageTicket_AsRequester_ThrowsUnauthorized()
    {
        _currentUser.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Requester)).Returns(true);

        var useCase = new TriageTicketUseCase(_writer.Object, _currentUser.Object, _clock.Object);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            useCase.ExecuteAsync(new(_ticketId, Impact.High, Urgency.High)));
    }

    [Fact]
    public async Task TriageTicket_WhenTicketNotFound_ThrowsNotFound()
    {
        _writer.Setup(w => w.GetForUpdateAsync(_ticketId, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket?)null);

        var useCase = new TriageTicketUseCase(_writer.Object, _currentUser.Object, _clock.Object);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            useCase.ExecuteAsync(new(_ticketId, Impact.Medium, Urgency.Low)));
    }

    [Fact]
    public async Task TriageTicket_WhenRevisionStale_ThrowsConcurrencyConflict()
    {
        var ticket = CreateTestTicket();
        _writer.Setup(w => w.GetForUpdateAsync(_ticketId, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(2u);

        var useCase = new TriageTicketUseCase(_writer.Object, _currentUser.Object, _clock.Object);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            useCase.ExecuteAsync(new(_ticketId, Impact.High, Urgency.High, ExpectedRevision: 1u)));
    }
}
