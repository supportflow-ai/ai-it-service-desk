using ServiceDesk.Modules.Ticketing.Domain;
using Xunit;

namespace ServiceDesk.Domain.Tests.Ticketing;

public class TicketTransferTests
{
    private static Ticket CreateTicket(Guid? requesterId = null) =>
        Ticket.Create(
            requesterId ?? Guid.NewGuid(),
            "Vấn đề mạng văn phòng",
            "Không thể truy cập máy in và mạng LAN nội bộ.",
            TicketCategory.NET.ToString(),
            DateTime.UtcNow);

    [Fact]
    public void TransferToTeam_WhenTargetTeamIsEmpty_ThrowsArgumentException()
    {
        var ticket = CreateTicket();
        Assert.Throws<ArgumentException>(() =>
            ticket.TransferToTeam(Guid.Empty, "Lý do", false, DateTime.UtcNow));
    }

    [Fact]
    public void TransferToTeam_WhenReasonIsBlank_ThrowsArgumentException()
    {
        var ticket = CreateTicket();
        var targetTeam = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() =>
            ticket.TransferToTeam(targetTeam, "   ", false, DateTime.UtcNow));
    }

    [Fact]
    public void TransferToTeam_WhenTicketIsClosed_ThrowsInvalidOperationException()
    {
        var ticket = CreateTicket();
        ticket.Claim(Guid.NewGuid(), DateTime.UtcNow);
        ticket.Resolve(DateTime.UtcNow);
        ticket.Close(DateTime.UtcNow);

        var targetTeam = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() =>
            ticket.TransferToTeam(targetTeam, "Chuyển đội", false, DateTime.UtcNow));
    }

    [Fact]
    public void TransferToTeam_WhenTargetIsSameTeam_ThrowsInvalidOperationException()
    {
        var ticket = CreateTicket();
        Assert.Throws<InvalidOperationException>(() =>
            ticket.TransferToTeam(SupportTeam.ManualTriageId, "Chuyển đội", false, DateTime.UtcNow));
    }

    [Fact]
    public void TransferToTeam_WhenValidAndAssigneeNotInNewTeam_ResetsAssignee()
    {
        var ticket = CreateTicket();
        var agentId = Guid.NewGuid();
        ticket.Assign(agentId, DateTime.UtcNow);
        Assert.Equal(TicketStatus.Assigned, ticket.Status);
        Assert.Equal(agentId, ticket.AssignedToId);

        var newTeamId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        ticket.TransferToTeam(newTeamId, "Chuyển sang nhóm Network", targetTeamHasCurrentAssignee: false, now);

        Assert.Equal(newTeamId, ticket.SupportTeamId);
        Assert.Null(ticket.AssignedToId);
        Assert.Null(ticket.AssignedAt);
        Assert.Equal(TicketStatus.Submitted, ticket.Status);
    }

    [Fact]
    public void TransferToTeam_WhenValidAndAssigneeInNewTeam_KeepsAssignee()
    {
        var ticket = CreateTicket();
        var agentId = Guid.NewGuid();
        ticket.Claim(agentId, DateTime.UtcNow);
        Assert.Equal(TicketStatus.InProgress, ticket.Status);

        var newTeamId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        ticket.TransferToTeam(newTeamId, "Chuyển sang nhóm Network cấp cao", targetTeamHasCurrentAssignee: true, now);

        Assert.Equal(newTeamId, ticket.SupportTeamId);
        Assert.Equal(agentId, ticket.AssignedToId);
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
    }
}
