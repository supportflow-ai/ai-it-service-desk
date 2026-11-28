using System.Text.Json;
using ServiceDesk.Modules.Ticketing.Domain;
using Xunit;

namespace ServiceDesk.Domain.Tests.Ticketing;

public sealed class SlaConsistencyTests
{
    private static readonly DateTime Start = new(2026, 11, 28, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(true, -1)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(false, -1)]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    public void ReadSnapshotAndPersistedMarkersAgreeAtDeadlineBoundaries(bool firstResponse, int ticks)
    {
        var ticket = Create();
        if (!firstResponse) ticket.RecordFirstResponse(Start.AddMinutes(1));
        var at = Start.AddMinutes(firstResponse ? 60 : 240).AddTicks(ticks);
        var before = JsonSerializer.Serialize(ticket);
        var read = SlaEvaluator.Evaluate(ticket, at);
        Assert.Equal(before, JsonSerializer.Serialize(ticket));
        Assert.Equal(DateTimeKind.Utc, read.EvaluatedAt.Kind);
        Assert.Equal(ticks > 0, firstResponse ? read.FirstResponse!.IsBreached : read.Resolution!.IsBreached);
        ticket.EvaluateSlaBreach(at);
        Assert.Equal(read.FirstResponse!.IsBreached, ticket.FirstResponseBreached);
        Assert.Equal(read.Resolution!.IsBreached, ticket.ResolutionBreached);
    }

    [Fact]
    public void MultiplePausesAndResolvedTimeAreExcludedAfterReopen()
    {
        var ticket = Create();
        ticket.Claim(Guid.NewGuid(), Start.AddMinutes(1));
        ticket.MarkPending(TicketStatus.PendingUser, Start.AddMinutes(10));
        ticket.ResumeFromPending(Start.AddMinutes(20));
        ticket.MarkPending(TicketStatus.PendingExternal, Start.AddMinutes(30));
        ticket.ResumeFromPending(Start.AddMinutes(50));
        ticket.Resolve(Start.AddMinutes(70));
        Assert.Equal(TimeSpan.FromMinutes(40), ticket.GetResolutionElapsed(Start.AddDays(1)));
        Assert.False(SlaEvaluator.Evaluate(ticket, Start.AddDays(1)).Resolution!.IsBreached);
        ticket.Reopen(Start.AddDays(1));
        var deadline = Start.AddDays(1).AddMinutes(200);
        Assert.False(SlaEvaluator.Evaluate(ticket, deadline).Resolution!.IsBreached);
        Assert.True(SlaEvaluator.Evaluate(ticket, deadline.AddTicks(1)).Resolution!.IsBreached);
        ticket.EvaluateSlaBreach(deadline.AddTicks(1));
        Assert.True(ticket.ResolutionBreached);
        ticket.UpdateSlaPriority(Priority.P2, deadline.AddTicks(2));
        Assert.True(SlaEvaluator.Evaluate(ticket, deadline.AddTicks(2)).Resolution!.IsBreached);
    }

    private static Ticket Create()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "SLA consistency", "Deterministic SLA boundaries", TicketCategories.NET, Start);
        ticket.SetTriage(Impact.High, Urgency.High, Start);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P1), Start);
        return ticket;
    }
}
