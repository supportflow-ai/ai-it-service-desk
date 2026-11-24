using Moq;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Application.TransferTicket;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public class TransferTicketUseCaseTests
{
    private readonly Mock<ITicketWriter> _writer = new();
    private readonly Mock<ISupportTeamStore> _supportTeamStore = new();
    private readonly Mock<ISupportTeamMembershipReader> _membershipReader = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IClock> _clock = new();

    private readonly Guid _callerId = Guid.NewGuid();
    private readonly Guid _ticketId = Guid.NewGuid();
    private readonly Guid _targetTeamId = Guid.NewGuid();

    public TransferTicketUseCaseTests()
    {
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(_callerId.ToString());
        _currentUser.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _clock.SetupGet(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task TransferTicket_WhenCallerNotStaff_ThrowsUnauthorized()
    {
        _currentUser.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var useCase = new TransferTicketUseCase(
            _writer.Object,
            _supportTeamStore.Object,
            _membershipReader.Object,
            _currentUser.Object,
            _clock.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            useCase.ExecuteAsync(new(_ticketId, new(_targetTeamId, "Chuyển nhóm"))));
    }

    [Fact]
    public async Task TransferTicket_WhenTargetTeamNotFound_ThrowsArgumentException()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Tiêu đề", "Mô tả nội dung dài", "NET", DateTime.UtcNow);
        _writer.Setup(w => w.GetForUpdateAsync(_ticketId, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        _currentUser.SetupGet(u => u.Roles).Returns([RoleNames.Admin]);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(true);

        _supportTeamStore.Setup(s => s.GetTeamByIdAsync(_targetTeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SupportTeam?)null);

        var useCase = new TransferTicketUseCase(
            _writer.Object,
            _supportTeamStore.Object,
            _membershipReader.Object,
            _currentUser.Object,
            _clock.Object);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(new(_ticketId, new(_targetTeamId, "Chuyển nhóm"))));
    }

    [Fact]
    public async Task TransferTicket_WhenValidAsAdmin_TransfersAndSavesAudit()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Tiêu đề", "Mô tả nội dung dài", "NET", DateTime.UtcNow);
        _writer.Setup(w => w.GetForUpdateAsync(_ticketId, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(1);
        _writer.Setup(w => w.UpdateTicketWithAuditAsync(ticket, It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDto(
                ticket.Id, "TICKET-01", ticket.Title, ticket.Description, ticket.CategoryId,
                (int)ticket.Status, ticket.CreatedAt));

        _currentUser.SetupGet(u => u.Roles).Returns([RoleNames.Admin]);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(true);

        var targetTeam = SupportTeam.Create("NETWORK", "Network Support", id: _targetTeamId);
        _supportTeamStore.Setup(s => s.GetTeamByIdAsync(_targetTeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetTeam);

        var useCase = new TransferTicketUseCase(
            _writer.Object,
            _supportTeamStore.Object,
            _membershipReader.Object,
            _currentUser.Object,
            _clock.Object);

        var result = await useCase.ExecuteAsync(new(_ticketId, new(_targetTeamId, "Chuyển sang nhóm chuyên môn", 1)));

        Assert.Equal(targetTeam.Id, ticket.SupportTeamId);
        _writer.Verify(w => w.UpdateTicketWithAuditAsync(ticket, It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
