using System.Text.Json;
using ServiceDesk.Modules.Ticketing.Domain;
using Xunit;

namespace ServiceDesk.Domain.Tests.Ticketing;

public sealed class TicketTransitionMatrixTests
{
    private static readonly DateTime Start = new(2026, 11, 28, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Agent = Guid.NewGuid();
    private static readonly TicketStatus[] Open = [TicketStatus.Submitted, TicketStatus.Assigned, TicketStatus.InProgress, TicketStatus.PendingUser, TicketStatus.PendingExternal];

    public static IEnumerable<object[]> Transitions()
    {
        var rules = new (string Command, TicketStatus[] From, TicketStatus? Target)[]
        {
            ("update", [TicketStatus.Submitted], null),
            ("assign", Open, null),
            ("unassign", [TicketStatus.Assigned], TicketStatus.Submitted),
            ("claim", [TicketStatus.Submitted, TicketStatus.Assigned], TicketStatus.InProgress),
            ("pending-user", [TicketStatus.InProgress], TicketStatus.PendingUser),
            ("pending-external", [TicketStatus.InProgress], TicketStatus.PendingExternal),
            ("resume", [TicketStatus.PendingUser, TicketStatus.PendingExternal], TicketStatus.InProgress),
            ("resolve", [TicketStatus.InProgress, TicketStatus.PendingExternal], TicketStatus.Resolved),
            ("reopen", [TicketStatus.Resolved], TicketStatus.InProgress),
            ("close", [TicketStatus.Resolved], TicketStatus.Closed),
            ("cancel", [TicketStatus.Submitted], TicketStatus.Cancelled),
            ("transfer", Open, null)
        };
        foreach (var source in Open.Concat([TicketStatus.Resolved, TicketStatus.Closed, TicketStatus.Cancelled]))
        foreach (var (command, from, target) in rules)
        {
            var expected = target ?? source;
            if (command == "assign" && source == TicketStatus.Submitted) expected = TicketStatus.Assigned;
            if (command == "transfer" && source == TicketStatus.Assigned) expected = TicketStatus.Submitted;
            yield return [command, source, from.Contains(source), expected];
        }
    }

    [Theory]
    [MemberData(nameof(Transitions))]
    public void CommandsFollowCanonicalTransitionMatrixAndRejectWithoutMutation(string command, TicketStatus source, bool allowed, TicketStatus expected)
    {
        var ticket = At(source);
        var before = JsonSerializer.Serialize(ticket);
        var now = Start.AddHours(3);
        Action apply = command switch
        {
            "update" => () => ticket.Update("New title", "Updated description of ticket", TicketCategories.VPN, now),
            "assign" => () => ticket.Assign(Agent, now),
            "unassign" => () => ticket.Unassign(now),
            "claim" => () => ticket.Claim(Agent, now),
            "pending-user" => () => ticket.MarkPending(TicketStatus.PendingUser, now),
            "pending-external" => () => ticket.MarkPending(TicketStatus.PendingExternal, now),
            "resume" => () => ticket.ResumeFromPending(now),
            "resolve" => () => ticket.Resolve(now),
            "reopen" => () => ticket.Reopen(now),
            "close" => () => ticket.Close(now),
            "cancel" => () => ticket.Cancel(now),
            _ => () => ticket.TransferToTeam(Guid.NewGuid(), "Transfer to another team", false, now)
        };
        if (!allowed)
        {
            Assert.Throws<InvalidOperationException>(apply);
            Assert.Equal(before, JsonSerializer.Serialize(ticket));
            return;
        }
        apply();
        Assert.Equal(expected, ticket.Status);
        Assert.Equal(now, ticket.UpdatedAt);
        Assert.NotEqual(TicketStatus.Draft, ticket.Status);
        Assert.NotEqual(TicketStatus.Triaged, ticket.Status);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(99, 1)]
    [InlineData(1, -1)]
    [InlineData(1, 99)]
    public void InvalidTriageValuesLeaveTheEntireAggregateUnchanged(int impact, int urgency)
    {
        var ticket = At(TicketStatus.InProgress);
        var before = JsonSerializer.Serialize(ticket);
        Assert.Throws<ArgumentOutOfRangeException>(() => ticket.SetTriage((Impact)impact, (Urgency)urgency, Start.AddHours(3), TicketCategories.VPN));
        Assert.Equal(before, JsonSerializer.Serialize(ticket));
    }

    private static Ticket At(TicketStatus status)
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Initial title", "Initial ticket description", TicketCategories.VPN, Start);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), Start);
        if (status == TicketStatus.Submitted) return ticket;
        if (status == TicketStatus.Cancelled) { ticket.Cancel(Start.AddMinutes(1)); return ticket; }
        if (status == TicketStatus.Assigned) { ticket.Assign(Agent, Start.AddMinutes(1)); return ticket; }
        ticket.Claim(Agent, Start.AddMinutes(1));
        if (status is TicketStatus.PendingUser or TicketStatus.PendingExternal) ticket.MarkPending(status, Start.AddMinutes(2));
        if (status is TicketStatus.Resolved or TicketStatus.Closed) ticket.Resolve(Start.AddMinutes(2));
        if (status == TicketStatus.Closed) ticket.Close(Start.AddMinutes(3));
        return ticket;
    }
}
