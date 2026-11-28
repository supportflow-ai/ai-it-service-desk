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

/// <summary>G1-01: quyền đọc/ghi phiếu qua luồng AI do Ticketing quyết định, không dùng AllTickets vô điều kiện.</summary>
public class TicketTriageBridgeTests
{
    private readonly Mock<ITicketQueries> _queries = new();
    private readonly Mock<ITicketWriter> _writer = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _ticketId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 11, 28, 8, 0, 0, TimeSpan.Zero);

    public TicketTriageBridgeTests() => _clock.SetupGet(c => c.UtcNow).Returns(_now);

    private TicketTriageBridge CreateBridge() => new(_queries.Object, _writer.Object, _currentUser.Object, _clock.Object);

    private void SetupUser(string role)
    {
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(_actorId.ToString());
        _currentUser.SetupGet(u => u.Roles).Returns([role]);
        _currentUser.Setup(u => u.IsInRole(It.IsAny<string>())).Returns((string r) => r == role);
    }

    private TicketDto Dto(Guid requesterId) =>
        new(_ticketId, "IT-1", "VPN down", "Cannot connect from home", TicketCategories.VPN, 0, _now.UtcDateTime, RequesterId: requesterId, Revision: 7);

    [Fact]
    public async Task Snapshot_AgentInScope_CanTriageUsingAgentScope()
    {
        SetupUser(RoleNames.Agent);
        _queries.Setup(q => q.GetByIdAsync(_ticketId, It.Is<TicketReadScope>(s => s is TicketReadScope.AgentTickets && !((TicketReadScope.AgentTickets)s).IncludeOwnTickets), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Dto(Guid.NewGuid()));

        var snapshot = await CreateBridge().GetSnapshotAsync(_ticketId);

        Assert.NotNull(snapshot);
        Assert.True(snapshot.CanTriage);
        Assert.Equal(7, snapshot.Revision);
        _queries.Verify(q => q.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<TicketReadScope.AllTickets>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Snapshot_AgentOutOfScopeButOwner_GetsPublicAccessOnly()
    {
        SetupUser(RoleNames.Agent);
        _queries.Setup(q => q.GetByIdAsync(_ticketId, It.IsAny<TicketReadScope.AgentTickets>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TicketDto?)null);
        _queries.Setup(q => q.GetByIdAsync(_ticketId, It.Is<TicketReadScope>(s => s is TicketReadScope.OwnTickets), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Dto(_actorId));

        var snapshot = await CreateBridge().GetSnapshotAsync(_ticketId);

        Assert.NotNull(snapshot);
        Assert.False(snapshot.CanTriage);
    }

    [Fact]
    public async Task Snapshot_AgentOutOfScopeAndNotOwner_IsConcealed()
    {
        SetupUser(RoleNames.Agent);
        _queries.Setup(q => q.GetByIdAsync(_ticketId, It.IsAny<TicketReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TicketDto?)null);

        Assert.Null(await CreateBridge().GetSnapshotAsync(_ticketId));
    }

    [Fact]
    public async Task Snapshot_Requester_UsesOwnScopeAndCannotTriage()
    {
        SetupUser(RoleNames.Requester);
        _queries.Setup(q => q.GetByIdAsync(_ticketId, It.Is<TicketReadScope>(s => s is TicketReadScope.OwnTickets), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Dto(_actorId));

        var snapshot = await CreateBridge().GetSnapshotAsync(_ticketId);

        Assert.NotNull(snapshot);
        Assert.False(snapshot.CanTriage);
        _queries.Verify(q => q.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<TicketReadScope.AgentTickets>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Snapshot_Anonymous_IsRejected()
    {
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreateBridge().GetSnapshotAsync(_ticketId));
        _queries.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Apply_Requester_IsForbiddenBeforeLoadingTicket()
    {
        SetupUser(RoleNames.Requester);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateBridge().ApplyTriageDecisionAsync(_ticketId, Impact.High, Urgency.High, null, "x", 1u));
        _writer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Apply_AgentOutOfScope_IsNotFoundAndWritesNothing()
    {
        SetupUser(RoleNames.Agent);
        _writer.Setup(w => w.GetForUpdateAsync(_ticketId, It.IsAny<TicketReadScope.AgentTickets>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            CreateBridge().ApplyTriageDecisionAsync(_ticketId, Impact.High, Urgency.High, null, "x", 1u));
        _writer.Verify(w => w.UpdateTicketWithAuditAsync(It.IsAny<Ticket>(), It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Apply_AgentInScope_WritesCategoryImpactUrgencyWithAuditByCurrentUser()
    {
        SetupUser(RoleNames.Agent);
        var ticket = Ticket.Create(Guid.NewGuid(), "VPN down", "Cannot connect from home", TicketCategories.VPN, _now.UtcDateTime);
        _writer.Setup(w => w.GetForUpdateAsync(_ticketId, It.IsAny<TicketReadScope.AgentTickets>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(1u);
        TicketComment? audit = null;
        _writer.Setup(w => w.UpdateTicketWithAuditAsync(ticket, It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()))
            .Callback<Ticket, TicketComment, CancellationToken>((_, c, _) => audit = c)
            .ReturnsAsync(Dto(ticket.RequesterId));

        await CreateBridge().ApplyTriageDecisionAsync(_ticketId, Impact.High, Urgency.High, TicketCategories.SEC, "Chấp nhận", 1u);

        Assert.Equal(TicketCategories.SEC, ticket.CategoryId);
        Assert.Equal(Priority.P1, ticket.Priority);
        Assert.NotNull(audit);
        Assert.Equal(_actorId, audit.AuthorId);
        Assert.Contains("VPN → SEC", audit.Content);
    }

    [Fact]
    public async Task Apply_StaleRevision_IsConflictAndWritesNothing()
    {
        SetupUser(RoleNames.Admin);
        var ticket = Ticket.Create(Guid.NewGuid(), "VPN down", "Cannot connect from home", TicketCategories.VPN, _now.UtcDateTime);
        _writer.Setup(w => w.GetForUpdateAsync(_ticketId, It.IsAny<TicketReadScope.AllTickets>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        _writer.Setup(w => w.GetRevision(ticket)).Returns(2u);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            CreateBridge().ApplyTriageDecisionAsync(_ticketId, Impact.High, Urgency.High, null, "x", 1u));
        Assert.Equal(Impact.Medium, ticket.Impact);
        _writer.Verify(w => w.UpdateTicketWithAuditAsync(It.IsAny<Ticket>(), It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
