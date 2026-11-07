using Microsoft.EntityFrameworkCore;
using Npgsql;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.Modules.Ticketing.Infrastructure;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class TicketAuditPersistenceTests(TicketIdempotencyPostgresFixture fixture)
    : IClassFixture<TicketIdempotencyPostgresFixture>
{
    private static readonly DateTime Now = new(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task UpdateWithTimeline_PersistsBoth_AndReturnsNewRevision()
    {
        var id = await CreateTicketAsync();
        var agent = Guid.NewGuid();
        await using var context = fixture.CreateContext();
        var writer = new PostgresTicketWriter(context);
        var ticket = (await writer.GetForUpdateAsync(id, default))!;
        var previousRevision = writer.GetRevision(ticket);
        ticket.Assign(agent, Now);
        var audit = Audit(ticket.Id, agent);

        var result = await writer.UpdateTicketWithAuditAsync(ticket, audit, default);

        Assert.NotEqual(previousRevision, result.Revision);
        await using var verification = fixture.CreateContext();
        var saved = await verification.Tickets.SingleAsync(value => value.Id == id);
        Assert.Equal(TicketStatus.Assigned, saved.Status);
        Assert.Equal(agent, saved.AssignedToId);
        Assert.Equal(id, (await verification.TicketComments.SingleAsync(value => value.Id == audit.Id)).TicketId);
    }

    [Fact]
    public async Task DeferredAuditFailureAtCommit_RollsBackTicketAndTimeline()
    {
        var id = await CreateTicketAsync();
        // Constraint trigger chỉ tồn tại trong database dùng một lần của fixture.
        // INITIALLY DEFERRED làm lỗi phát sinh lúc commit, sau khi các câu lệnh ghi đã chạy.
        await using (var setup = fixture.CreateContext())
        {
            await setup.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION public.reject_atomic_audit() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Injected deferred audit failure'; END; $$;
                CREATE CONSTRAINT TRIGGER reject_atomic_audit
                AFTER INSERT ON public.ticket_comments DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION public.reject_atomic_audit();
                """);
        }
        try
        {
            await using (var context = fixture.CreateContext())
            {
                var writer = new PostgresTicketWriter(context);
                var ticket = (await writer.GetForUpdateAsync(id, default))!;
                ticket.Assign(Guid.NewGuid(), Now);
                var exception = await Record.ExceptionAsync(() => writer.UpdateTicketWithAuditAsync(ticket, Audit(id), default));
                Assert.NotNull(exception);
                var databaseError = exception as PostgresException ?? exception.InnerException as PostgresException;
                Assert.NotNull(databaseError);
                Assert.Equal(PostgresErrorCodes.RaiseException, databaseError.SqlState);
            }

            await using var verification = fixture.CreateContext();
            var saved = await verification.Tickets.SingleAsync(value => value.Id == id);
            Assert.Equal(TicketStatus.Submitted, saved.Status);
            Assert.Null(saved.AssignedToId);
            Assert.False(await verification.TicketComments.AnyAsync(value => value.TicketId == id));
        }
        finally
        {
            await using var cleanup = fixture.CreateContext();
            await cleanup.Database.ExecuteSqlRawAsync("""
                DROP TRIGGER reject_atomic_audit ON public.ticket_comments;
                DROP FUNCTION public.reject_atomic_audit();
                """);
        }
    }

    [Fact]
    public async Task StaleTicket_ProducesConflict_WithoutOrphanTimeline()
    {
        var id = await CreateTicketAsync();
        await using var first = fixture.CreateContext();
        await using var second = fixture.CreateContext();
        var firstWriter = new PostgresTicketWriter(first);
        var secondWriter = new PostgresTicketWriter(second);
        var winner = (await firstWriter.GetForUpdateAsync(id, default))!;
        var stale = (await secondWriter.GetForUpdateAsync(id, default))!;
        var agent = Guid.NewGuid();
        winner.Assign(agent, Now);
        var firstAudit = Audit(id, agent);
        await firstWriter.UpdateTicketWithAuditAsync(winner, firstAudit, default);
        stale.Assign(Guid.NewGuid(), Now);
        var staleAudit = Audit(id);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => secondWriter.UpdateTicketWithAuditAsync(stale, staleAudit, default));

        await using var verification = fixture.CreateContext();
        Assert.Equal(agent, (await verification.Tickets.SingleAsync(value => value.Id == id)).AssignedToId);
        var timeline = await verification.TicketComments.Where(value => value.TicketId == id).ToListAsync();
        Assert.Equal(firstAudit.Id, Assert.Single(timeline).Id);
    }

    [Fact]
    public async Task AuditForAnotherTicket_IsRejectedWithoutSaving()
    {
        var id = await CreateTicketAsync();
        await using var context = fixture.CreateContext();
        var writer = new PostgresTicketWriter(context);
        var ticket = (await writer.GetForUpdateAsync(id, default))!;
        ticket.Assign(Guid.NewGuid(), Now);

        await Assert.ThrowsAsync<ArgumentException>(() => writer.UpdateTicketWithAuditAsync(ticket, Audit(Guid.NewGuid()), default));

        await using var verification = fixture.CreateContext();
        Assert.Equal(TicketStatus.Submitted, (await verification.Tickets.SingleAsync(value => value.Id == id)).Status);
        Assert.False(await verification.TicketComments.AnyAsync(value => value.TicketId == id));
    }

    private async Task<Guid> CreateTicketAsync()
    {
        await using var context = fixture.CreateContext();
        var ticket = Ticket.Create(Guid.NewGuid(), "Lỗi VPN", "Mô tả lỗi đủ độ dài", TicketCategories.VPN, Now);
        await new PostgresTicketWriter(context).CreateAsync(ticket, null, default);
        return ticket.Id;
    }

    private static TicketComment Audit(Guid ticketId, Guid? actor = null) =>
        TicketComment.CreatePublic(ticketId, actor ?? Guid.NewGuid(), "Hệ thống", "System", "Sự kiện kiểm thử nguyên tử", Now);
}
