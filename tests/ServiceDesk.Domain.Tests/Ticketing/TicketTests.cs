using FluentAssertions;
using ServiceDesk.Domain.Ticketing;
using Xunit;

namespace ServiceDesk.Domain.Tests.Ticketing;

public class TicketTests
{
    [Fact]
    public void Create_ValidInput_CalculatesPriorityAndStartsSubmitted()
    {
        var createdAt = new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

        var ticket = Ticket.Create("requester-1", "VPN unavailable", "Cannot connect", "Network", Impact.High, Urgency.Medium, createdAt);

        ticket.Status.Should().Be(TicketStatus.Submitted);
        ticket.Priority.Should().Be(Priority.P2);
        ticket.RequesterId.Should().Be("requester-1");
        ticket.AssigneeId.Should().BeNull();
        ticket.CreatedAt.Should().Be(createdAt);
        ticket.UpdatedAt.Should().Be(createdAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_BoundaryTimestamp_Throws(bool minimum)
    {
        var timestamp = minimum ? DateTimeOffset.MinValue : DateTimeOffset.MaxValue;

        var action = () => Ticket.Create("requester-1", "VPN unavailable", "Cannot connect", "Network", Impact.High, Urgency.Medium, timestamp);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Update_Submitted_RecalculatesPriority()
    {
        var ticket = CreateSubmittedTicket();

        ticket.Update("New title", "New description", "Hardware", Impact.Low, Urgency.Low, DateTimeOffset.UtcNow);

        ticket.Priority.Should().Be(Priority.P4);
        ticket.Title.Should().Be("New title");
    }

    [Fact]
    public void Update_BoundaryTimestamp_Throws()
    {
        var ticket = CreateSubmittedTicket();

        var action = () => ticket.Update("New title", "New description", "Hardware", Impact.Low, Urgency.Low, DateTimeOffset.MinValue);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Claim_SubmittedWithoutAssignee_AssignsAgentAndStartsWork()
    {
        var ticket = CreateSubmittedTicket();

        ticket.Claim("agent-1", DateTimeOffset.UtcNow);

        ticket.Status.Should().Be(TicketStatus.InProgress);
        ticket.AssigneeId.Should().Be("agent-1");
    }

    [Fact]
    public void Resolve_Reopen_Close_UsesAllowedWorkflow()
    {
        var ticket = CreateClaimedTicket();

        ticket.Resolve(DateTimeOffset.UtcNow);
        ticket.Reopen(DateTimeOffset.UtcNow);
        ticket.Resolve(DateTimeOffset.UtcNow);
        ticket.Close(DateTimeOffset.UtcNow);

        ticket.Status.Should().Be(TicketStatus.Closed);
        ticket.ResolvedAt.Should().NotBeNull();
        ticket.ClosedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData(TicketStatus.Submitted)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Resolved)]
    public void Transition_BoundaryTimestamp_Throws(TicketStatus status)
    {
        var ticket = status switch
        {
            TicketStatus.Submitted => CreateSubmittedTicket(),
            TicketStatus.InProgress => CreateClaimedTicket(),
            TicketStatus.Resolved => CreateResolvedTicket(),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

        Action action = status switch
        {
            TicketStatus.Submitted => () => ticket.Claim("agent-1", DateTimeOffset.MinValue),
            TicketStatus.InProgress => () => ticket.Resolve(DateTimeOffset.MinValue),
            TicketStatus.Resolved => () => ticket.Close(DateTimeOffset.MinValue),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Cancel_Submitted_BecomesTerminal()
    {
        var ticket = CreateSubmittedTicket();

        ticket.Cancel(DateTimeOffset.UtcNow);

        ticket.Status.Should().Be(TicketStatus.Cancelled);
        ticket.CancelledAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public void AddComment_TerminalTicket_Throws(TicketStatus terminalStatus)
    {
        var ticket = terminalStatus == TicketStatus.Closed ? CreateClosedTicket() : CreateCancelledTicket();

        var action = () => ticket.AddComment("requester-1", "More detail", DateTimeOffset.UtcNow);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Claim_AlreadyAssigned_Throws()
    {
        var ticket = CreateClaimedTicket();

        var action = () => ticket.Claim("agent-2", DateTimeOffset.UtcNow);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Update_NonSubmitted_Throws()
    {
        var ticket = CreateClaimedTicket();

        var action = () => ticket.Update("Title", "Description", "Network", Impact.Low, Urgency.Low, DateTimeOffset.UtcNow);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddComment_NonTerminal_AddsImmutableComment()
    {
        var ticket = CreateSubmittedTicket();
        var createdAt = DateTimeOffset.UtcNow;

        ticket.AddComment("requester-1", "More detail", createdAt);

        var comment = ticket.Comments.Should().ContainSingle().Subject;
        comment.TicketId.Should().Be(ticket.Id);
        comment.AuthorId.Should().Be("requester-1");
        comment.Content.Should().Be("More detail");
        comment.CreatedAt.Should().Be(createdAt);
    }

    private static Ticket CreateSubmittedTicket() => Ticket.Create(
        "requester-1", "VPN unavailable", "Cannot connect", "Network", Impact.High, Urgency.Medium, DateTimeOffset.UtcNow);

    private static Ticket CreateClaimedTicket()
    {
        var ticket = CreateSubmittedTicket();
        ticket.Claim("agent-1", DateTimeOffset.UtcNow);
        return ticket;
    }

    private static Ticket CreateResolvedTicket()
    {
        var ticket = CreateClaimedTicket();
        ticket.Resolve(DateTimeOffset.UtcNow);
        return ticket;
    }

    private static Ticket CreateClosedTicket()
    {
        var ticket = CreateClaimedTicket();
        ticket.Resolve(DateTimeOffset.UtcNow);
        ticket.Close(DateTimeOffset.UtcNow);
        return ticket;
    }

    private static Ticket CreateCancelledTicket()
    {
        var ticket = CreateSubmittedTicket();
        ticket.Cancel(DateTimeOffset.UtcNow);
        return ticket;
    }
}
