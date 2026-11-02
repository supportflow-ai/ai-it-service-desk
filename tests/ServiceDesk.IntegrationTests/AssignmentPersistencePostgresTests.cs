using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Domain.Ticketing;
using ServiceDesk.Infrastructure.Ticketing;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class AssignmentPersistencePostgresTests(TicketIdempotencyPostgresFixture fixture)
    : IClassFixture<TicketIdempotencyPostgresFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuditInsertFailure_RollsBackTicketAssignment(bool unassign)
    {
        var requesterId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        Guid ticketId;
        await using (var seed = fixture.CreateContext())
        {
            var ticket = Ticket.Create(requesterId, "Title", "Valid description here", TicketCategories.VPN, DateTime.UtcNow);
            if (unassign) ticket.Assign(assigneeId, DateTime.UtcNow);
            seed.Tickets.Add(ticket);
            await seed.SaveChangesAsync();
            ticketId = ticket.Id;
        }

        await using (var setup = fixture.CreateContext())
        {
            await setup.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS reject_assignment_audit ON public.ticket_comments;");
            await setup.Database.ExecuteSqlRawAsync("""
                CREATE OR REPLACE FUNCTION public.reject_assignment_audit() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Injected assignment audit failure'; END; $$;
                CREATE TRIGGER reject_assignment_audit
                BEFORE INSERT ON public.ticket_comments
                FOR EACH ROW EXECUTE FUNCTION public.reject_assignment_audit();
                """);
        }

        try
        {
            await using var context = fixture.CreateContext();
            var ticket = await context.Tickets.SingleAsync(item => item.Id == ticketId);
            if (unassign) ticket.Unassign(DateTime.UtcNow);
            else ticket.Assign(assigneeId, DateTime.UtcNow);
            var audit = TicketComment.CreatePublic(ticket.Id, assigneeId, "Hệ thống", "System", "Assignment audit", DateTime.UtcNow);

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                new PostgresTicketWriter(context).UpdateTicketWithAuditAsync(ticket, audit, CancellationToken.None));
        }
        finally
        {
            await using var cleanup = fixture.CreateContext();
            await cleanup.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS reject_assignment_audit ON public.ticket_comments;");
            await cleanup.Database.ExecuteSqlRawAsync("DROP FUNCTION IF EXISTS public.reject_assignment_audit();");
        }

        await using var verify = fixture.CreateContext();
        var stored = await verify.Tickets.SingleAsync(item => item.Id == ticketId);
        stored.Status.Should().Be(unassign ? TicketStatus.Assigned : TicketStatus.Submitted);
        stored.AssignedToId.Should().Be(unassign ? assigneeId : null);
        (await verify.TicketComments.CountAsync(comment => comment.TicketId == ticketId)).Should().Be(0);
    }
}
