using Moq;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.Modules.Ticketing.Infrastructure;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class SlaReportConsistencyTests(TicketIdempotencyPostgresFixture fixture) : IClassFixture<TicketIdempotencyPostgresFixture>
{
    [Fact]
    public async Task ReportCountsMatchDomainMarkersIncludingPausedTickets()
    {
        var now = new DateTime(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc);
        var team = SupportTeam.Create($"SLA_{Guid.NewGuid():N}", "SLA report team");
        var tickets = new List<Ticket>();
        foreach (var hours in new[] { 2, 5, 10 })
        {
            var start = now.AddHours(-hours);
            var ticket = Ticket.Create(Guid.NewGuid(), "SLA report", "SLA report consistency regression", TicketCategories.NET, start);
            ticket.TransferToTeam(team.Id, "Route", false, start);
            ticket.SetTriage(Impact.High, Urgency.High, start);
            ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P1), start);
            if (hours == 10)
            {
                ticket.Claim(Guid.NewGuid(), start.AddMinutes(1));
                ticket.RecordFirstResponse(start.AddMinutes(1));
                ticket.MarkPending(TicketStatus.PendingExternal, start.AddMinutes(10));
            }
            var expected = SlaEvaluator.Evaluate(ticket, now);
            ticket.EvaluateSlaBreach(now);
            Assert.Equal(expected.FirstResponse!.IsBreached, ticket.FirstResponseBreached);
            Assert.Equal(expected.Resolution!.IsBreached, ticket.ResolutionBreached);
            tickets.Add(ticket);
        }
        await using (var db = fixture.CreateContext())
        {
            db.SupportTeams.Add(team);
            db.Tickets.AddRange(tickets);
            await db.SaveChangesAsync();
        }
        await using var fresh = fixture.CreateContext();
        var query = new PostgresTicketReportQueries(fresh, Mock.Of<IAgentDirectory>());
        var report = await query.GetSlaPerformanceAsync(new TicketReadScope.AllTickets(), null, null, team.Id);
        Assert.Equal(3, report.TotalTickets);
        Assert.Equal(tickets.Count(t => t.FirstResponseBreached), report.FirstResponseBreachedCount);
        Assert.Equal(tickets.Count(t => t.ResolutionBreached), report.ResolutionBreachedCount);
    }
}
