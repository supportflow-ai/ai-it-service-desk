using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.AIAssistance.Application.Contracts;
using ServiceDesk.Modules.AIAssistance.Application.Triage;
using ServiceDesk.Modules.AIAssistance.Domain;
using ServiceDesk.Modules.AIAssistance.Infrastructure;
using ServiceDesk.Modules.Ticketing.Application.TriageTicket;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.Modules.Ticketing.Infrastructure;
using ServiceDesk.Platform.Clock;
using ServiceDesk.Platform.Persistence;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Application;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class AiTriagePostgresTests(TicketIdempotencyPostgresFixture fixture)
    : IClassFixture<TicketIdempotencyPostgresFixture>
{
    private readonly Guid _reviewer = Guid.NewGuid();

    private sealed class Actor(Guid id) : ICurrentUser
    {
        public string UserId => id.ToString();
        public bool IsAuthenticated => true;
        public IReadOnlyList<string> Roles => ["Admin"];
        public bool IsInRole(string role) => Roles.Contains(role);
    }

    private DecideAiTriageUseCase Decide(ApplicationDbContext context)
    {
        var clock = new SystemClock();
        var actor = new Actor(_reviewer);
        return new(new TicketTriageBridge(new PostgresTicketQueries(context), new PostgresTicketWriter(context), actor, clock),
            new PostgresAiAnalysisStore(context), actor, clock);
    }

    private static AIAnalysis Analysis(Ticket ticket, long revision)
    {
        var analysis = AIAnalysis.CreateSucceeded(ticket.Id, revision,
            InputHashComputer.Compute(ticket.Title, ticket.Description), DateTimeOffset.UtcNow);
        analysis.AddSuggestion(AISuggestion.Create(analysis.Id, SuggestionType.Category, "NET", 0.9));
        analysis.AddSuggestion(AISuggestion.Create(analysis.Id, SuggestionType.Impact, "High", 0.8));
        analysis.AddSuggestion(AISuggestion.Create(analysis.Id, SuggestionType.Urgency, "High", 0.8));
        return analysis;
    }

    private async Task<(Guid Ticket, Guid Analysis, uint Revision)> SeedAsync()
    {
        await using var context = fixture.CreateContext();
        var ticket = Ticket.Create(Guid.NewGuid(), "VPN down", "Cannot connect from home", "VPN", DateTime.UtcNow);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();
        var revision = context.Entry(ticket).Property<uint>("xmin").CurrentValue;
        var analysis = Analysis(ticket, revision);
        await new PostgresAiAnalysisStore(context).AddAsync(analysis);
        return (ticket.Id, analysis.Id, revision);
    }

    [Fact]
    public async Task AnalysesAndSuggestionsSurviveNewContextAndPreserveHistory()
    {
        var seed = await SeedAsync();
        Guid secondId;
        await using (var context = fixture.CreateContext())
        {
            var ticket = await context.Tickets.SingleAsync(t => t.Id == seed.Ticket);
            var second = Analysis(ticket, seed.Revision);
            secondId = second.Id;
            await new PostgresAiAnalysisStore(context).AddAsync(second);
        }
        await using var replica = fixture.CreateContext();
        var latest = await new PostgresAiAnalysisStore(replica).GetLatestAsync(seed.Ticket);
        Assert.Equal(secondId, latest!.Id);
        Assert.Equal(3, latest.Suggestions.Count);
        Assert.Equal(2, await replica.AiAnalyses.CountAsync(a => a.TicketId == seed.Ticket));
        Assert.False(replica.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task FailedOutcomeAndValidationErrorsSurvivePostgresRoundTrip()
    {
        var seed = await SeedAsync();
        await using (var context = fixture.CreateContext())
        {
            var failed = AIAnalysis.CreateFailed(seed.Ticket, seed.Revision, new string('a', 64), DateTimeOffset.UtcNow,
                "invalid response", TriageOutcome.InvalidResponse, ["Invalid category"]);
            await new PostgresAiAnalysisStore(context).AddAsync(failed);
        }
        await using var replica = fixture.CreateContext();
        var read = await new PostgresAiAnalysisStore(replica).GetLatestAsync(seed.Ticket);
        Assert.Equal(TriageOutcome.InvalidResponse, read!.Outcome);
        Assert.Equal(new[] { "Invalid category" }, read.ValidationErrors);
    }

    [Fact]
    public async Task AcceptCommitsTicketDecisionAndAuditAndReplayAfterRestartDoesNotWriteAgain()
    {
        var seed = await SeedAsync();
        var command = new TriageDecisionCommand(seed.Ticket, TriageDecisionType.Accept,
            ExpectedRevision: seed.Revision, AnalysisId: seed.Analysis);
        await using (var first = fixture.CreateContext()) await Decide(first).ExecuteAsync(command);
        await using (var restarted = fixture.CreateContext()) await Decide(restarted).ExecuteAsync(command);
        await using var verify = fixture.CreateContext();
        var ticket = await verify.Tickets.SingleAsync(t => t.Id == seed.Ticket);
        var analysis = await verify.AiAnalyses.Include(a => a.Suggestions).SingleAsync(a => a.Id == seed.Analysis);
        Assert.Equal("NET", ticket.CategoryId);
        Assert.Equal(Impact.High, ticket.Impact);
        Assert.Equal(SuggestionDecision.Accepted, analysis.Decision);
        Assert.Equal(_reviewer, analysis.DecidedBy);
        Assert.Equal("VPN", analysis.Previous!.CategoryId);
        Assert.Equal("NET", analysis.Applied!.CategoryId);
        Assert.All(analysis.Suggestions, s => Assert.Equal(SuggestionDecision.Accepted, s.Decision));
        Assert.Equal(1, await verify.TicketComments.CountAsync(c => c.TicketId == seed.Ticket));
    }

    [Fact]
    public async Task ConcurrentIdenticalDecisionsCommitExactlyOnce()
    {
        var seed = await SeedAsync();
        var command = new TriageDecisionCommand(seed.Ticket, TriageDecisionType.Accept,
            ExpectedRevision: seed.Revision, AnalysisId: seed.Analysis);
        await using var first = fixture.CreateContext();
        await using var second = fixture.CreateContext();
        await Task.WhenAll(Decide(first).ExecuteAsync(command), Decide(second).ExecuteAsync(command));
        await using var verify = fixture.CreateContext();
        Assert.Equal(1, await verify.TicketComments.CountAsync(c => c.TicketId == seed.Ticket));
        Assert.Equal(SuggestionDecision.Accepted, (await verify.AiAnalyses.SingleAsync(a => a.Id == seed.Analysis)).Decision);
    }

    [Fact]
    public async Task ConflictingReplayIsRejectedAndRejectDoesNotChangeTicket()
    {
        var seed = await SeedAsync();
        await using (var first = fixture.CreateContext())
            await Decide(first).ExecuteAsync(new(seed.Ticket, TriageDecisionType.Reject, Reason: "Sai", AnalysisId: seed.Analysis));
        await using (var second = fixture.CreateContext())
            await Assert.ThrowsAsync<TriageDecisionConflictException>(() => Decide(second).ExecuteAsync(
                new(seed.Ticket, TriageDecisionType.Accept, ExpectedRevision: seed.Revision, AnalysisId: seed.Analysis)));
        await using var verify = fixture.CreateContext();
        var ticket = await verify.Tickets.SingleAsync(t => t.Id == seed.Ticket);
        Assert.Equal("VPN", ticket.CategoryId);
        Assert.Equal(seed.Revision, verify.Entry(ticket).Property<uint>("xmin").CurrentValue);
        Assert.False(await verify.TicketComments.AnyAsync(c => c.TicketId == seed.Ticket));
        Assert.Equal("Sai", (await verify.AiAnalyses.SingleAsync(a => a.Id == seed.Analysis)).DecisionReason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureSavingTicketOrDecisionRollsBackAllThreeRecords(bool failDecision)
    {
        var seed = await SeedAsync();
        await using (var failing = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                         .UseNpgsql(fixture.ConnectionString).AddInterceptors(new FailSave(failDecision)).Options))
            await Assert.ThrowsAsync<InvalidOperationException>(() => Decide(failing).ExecuteAsync(new(
                seed.Ticket, TriageDecisionType.Accept, ExpectedRevision: seed.Revision, AnalysisId: seed.Analysis)));
        await using var verify = fixture.CreateContext();
        Assert.Equal("VPN", (await verify.Tickets.SingleAsync(t => t.Id == seed.Ticket)).CategoryId);
        Assert.Equal(SuggestionDecision.Pending, (await verify.AiAnalyses.SingleAsync(a => a.Id == seed.Analysis)).Decision);
        Assert.False(await verify.TicketComments.AnyAsync(c => c.TicketId == seed.Ticket));
    }

    private sealed class FailSave(bool failDecision) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var deciding = eventData.Context!.ChangeTracker.Entries<AIAnalysis>()
                .Any(e => e.Entity.Decision != SuggestionDecision.Pending);
            if (deciding == failDecision) throw new InvalidOperationException("Lỗi ghi được tiêm có chủ đích.");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task AnalysisOfAnotherTicketAndStaleContentCannotBeApplied()
    {
        var seed = await SeedAsync();
        var other = await SeedAsync();
        uint currentRevision;
        await using (var context = fixture.CreateContext())
        {
            await Assert.ThrowsAsync<KeyNotFoundException>(() => Decide(context).ExecuteAsync(new(
                seed.Ticket, TriageDecisionType.Accept, ExpectedRevision: seed.Revision, AnalysisId: other.Analysis)));
            await context.Tickets.Where(t => t.Id == seed.Ticket)
                .ExecuteUpdateAsync(u => u.SetProperty(t => t.Title, "Nội dung mới"));
            currentRevision = await context.Tickets.Where(t => t.Id == seed.Ticket).Select(t => EF.Property<uint>(t, "xmin")).SingleAsync();
        }
        await using var stale = fixture.CreateContext();
        await Assert.ThrowsAsync<TriageDecisionConflictException>(() => Decide(stale).ExecuteAsync(new(
            seed.Ticket, TriageDecisionType.Accept, ExpectedRevision: currentRevision, AnalysisId: seed.Analysis)));
    }

    [Theory]
    [InlineData("remove")]
    [InlineData("deactivate")]
    [InlineData("transfer")]
    public async Task HitlTransactionKeepsAuthorizedScopeUntilCommit(string change)
    {
        var seed = await SeedAsync();
        var team = Guid.NewGuid();
        var agent = Guid.NewGuid();
        await using var first = fixture.CreateContext();
        await first.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.support_teams ("Id", code, name, is_triage_queue, is_active)
            VALUES ({team}, {team.ToString("N")}, 'Test', false, true);
            INSERT INTO public.support_team_members (team_id, user_id, is_lead) VALUES ({team}, {agent}, false);
            """);
        await first.Tickets.Where(t => t.Id == seed.Ticket).ExecuteUpdateAsync(u => u.SetProperty(t => t.SupportTeamId, team));
        await using var transaction = await first.Database.BeginTransactionAsync();
        Assert.NotNull(await new PostgresTicketWriter(first).GetForUpdateAsync(seed.Ticket,
            new TicketReadScope.AgentTickets(agent), default));
        await using var revoke = fixture.CreateContext();
        await using var revocation = await revoke.Database.BeginTransactionAsync();
        await revoke.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '200ms'");
        var error = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            if (change == "remove")
                await revoke.SupportTeamMembers.Where(m => m.TeamId == team && m.UserId == agent).ExecuteDeleteAsync();
            else if (change == "deactivate")
                await revoke.SupportTeams.Where(t => t.Id == team).ExecuteUpdateAsync(u => u.SetProperty(t => t.IsActive, false));
            else
                await revoke.Tickets.Where(t => t.Id == seed.Ticket)
                    .ExecuteUpdateAsync(u => u.SetProperty(t => t.SupportTeamId, SupportTeam.ManualTriageId));
        });
        // EF bọc lỗi PostgreSQL transient trong InvalidOperationException.
        Assert.Equal(PostgresErrorCodes.LockNotAvailable, Assert.IsType<PostgresException>(error.GetBaseException()).SqlState);
    }

    [Fact]
    public async Task ConcurrentDecisionLockTimeoutReturnsRetryLater()
    {
        var seed = await SeedAsync();
        await using var first = fixture.CreateContext();
        await using var second = fixture.CreateContext();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holding = new PostgresAiAnalysisStore(first).DecideAsync(seed.Ticket, seed.Analysis, async _ =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(15));
            return 0;
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.ThrowsAnyAsync<RetryLaterException>(() => new PostgresAiAnalysisStore(second)
                .DecideAsync(seed.Ticket, seed.Analysis, _ => Task.FromResult(0)));
        }
        finally { release.TrySetResult(); }
        await holding;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectCannotDecideClosedTicketOrMismatchedRevision(bool closed)
    {
        var seed = await SeedAsync();
        await using var context = fixture.CreateContext();
        if (closed)
            await context.Tickets.Where(t => t.Id == seed.Ticket)
                .ExecuteUpdateAsync(u => u.SetProperty(t => t.Status, TicketStatus.Closed));
        var error = await Record.ExceptionAsync(() => Decide(context).ExecuteAsync(new(seed.Ticket,
            TriageDecisionType.Reject, Reason: "Sai", ExpectedRevision: closed ? null : 0u, AnalysisId: seed.Analysis)));
        Assert.NotNull(error);
        await using var verify = fixture.CreateContext();
        Assert.Equal(SuggestionDecision.Pending, (await verify.AiAnalyses.SingleAsync(a => a.Id == seed.Analysis)).Decision);
    }

    [Fact]
    public async Task TwoReviewersCannotBothDecideTheSameAnalysis()
    {
        var seed = await SeedAsync();
        await using var first = fixture.CreateContext();
        await using var second = fixture.CreateContext();
        var command = new TriageDecisionCommand(seed.Ticket, TriageDecisionType.Accept, ExpectedRevision: seed.Revision, AnalysisId: seed.Analysis);
        var otherActor = new Actor(Guid.NewGuid());
        var clock = new SystemClock();
        var other = new DecideAiTriageUseCase(new TicketTriageBridge(new PostgresTicketQueries(second),
            new PostgresTicketWriter(second), otherActor, clock), new PostgresAiAnalysisStore(second), otherActor, clock);
        var errors = await Task.WhenAll(Record.ExceptionAsync(() => Decide(first).ExecuteAsync(command)),
            Record.ExceptionAsync(() => other.ExecuteAsync(command)));
        Assert.Single(errors, error => error is null);
        Assert.IsType<TriageDecisionConflictException>(Assert.Single(errors, error => error is not null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationOrLostConnectionAfterTicketSaveRollsBackDecisionAndAudit(bool disconnect)
    {
        var seed = await SeedAsync();
        using var cancellation = new CancellationTokenSource();
        await using (var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                         .UseNpgsql(fixture.ConnectionString).AddInterceptors(new InterruptCommit(fixture, cancellation, disconnect)).Options))
        {
            var error = await Record.ExceptionAsync(() => Decide(context).ExecuteAsync(new(seed.Ticket,
                TriageDecisionType.Accept, ExpectedRevision: seed.Revision, AnalysisId: seed.Analysis), cancellation.Token));
            Assert.NotNull(error);
        }
        await using var verify = fixture.CreateContext();
        Assert.Equal("VPN", (await verify.Tickets.SingleAsync(t => t.Id == seed.Ticket)).CategoryId);
        Assert.Equal(SuggestionDecision.Pending, (await verify.AiAnalyses.SingleAsync(a => a.Id == seed.Analysis)).Decision);
        Assert.False(await verify.TicketComments.AnyAsync(c => c.TicketId == seed.Ticket));
    }

    private sealed class InterruptCommit(TicketIdempotencyPostgresFixture fixture, CancellationTokenSource cancellation, bool disconnect)
        : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AIAnalysis>().Any(e => e.Entity.Decision != SuggestionDecision.Pending))
            {
                if (disconnect)
                {
                    var pid = ((NpgsqlConnection)eventData.Context.Database.GetDbConnection()).ProcessID;
                    await using var killer = fixture.CreateContext();
                    Assert.True(await killer.Database.SqlQuery<bool>($"SELECT pg_terminate_backend({pid}) AS \"Value\"").SingleAsync());
                }
                else cancellation.Cancel();
            }
            return result;
        }
    }

    [Fact]
    public async Task AuditInsertFailureAtDatabaseRollsBackTicketAndAnalysis()
    {
        var seed = await SeedAsync();
        await using var setup = fixture.CreateContext();
        await setup.Database.ExecuteSqlRawAsync("ALTER TABLE public.ticket_comments ADD CONSTRAINT ck_test_triage_audit CHECK (content NOT LIKE '[AI HITL Triage]%') NOT VALID");
        try
        {
            await using var context = fixture.CreateContext();
            var error = await Record.ExceptionAsync(() => Decide(context).ExecuteAsync(new(seed.Ticket,
                TriageDecisionType.Accept, ExpectedRevision: seed.Revision, AnalysisId: seed.Analysis)));
            Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(error!.GetBaseException()).SqlState);
            Assert.Equal("VPN", (await setup.Tickets.SingleAsync(t => t.Id == seed.Ticket)).CategoryId);
            Assert.Equal(SuggestionDecision.Pending, (await setup.AiAnalyses.SingleAsync(a => a.Id == seed.Analysis)).Decision);
            Assert.False(await setup.TicketComments.AnyAsync(c => c.TicketId == seed.Ticket));
        }
        finally { await setup.Database.ExecuteSqlRawAsync("ALTER TABLE public.ticket_comments DROP CONSTRAINT ck_test_triage_audit"); }
    }

    [Theory]
    [InlineData("foreign-key", PostgresErrorCodes.ForeignKeyViolation)]
    [InlineData("unique", PostgresErrorCodes.UniqueViolation)]
    [InlineData("score", PostgresErrorCodes.CheckViolation)]
    public async Task PostgreSqlEnforcesSuggestionConstraints(string constraint, string sqlState)
    {
        var seed = await SeedAsync();
        await using var context = fixture.CreateContext();
        var analysis = constraint == "foreign-key" ? Guid.NewGuid() : seed.Analysis;
        var score = constraint == "score" ? 1.5 : 0.8;
        var error = await Record.ExceptionAsync(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.ai_suggestions (id, analysis_id, type, suggested_value, model_score, decision)
            VALUES ({Guid.NewGuid()}, {analysis}, 'Category', 'VPN', {score}, 'Pending')
            """));
        Assert.Equal(sqlState, Assert.IsType<PostgresException>(error!.GetBaseException()).SqlState);
    }

    [Fact]
    public async Task UpgradeFromPreAiSchemaPreservesTicketAndAddsAiTables()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("ai_upgrade_tests").WithUsername("test").WithPassword("test").Build();
        await postgres.StartAsync();
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options);
        await context.GetService<IMigrator>().MigrateAsync("20261116180130_AddTicketSlaSupport");
        var ticket = Ticket.Create(Guid.NewGuid(), "Trước migration", "Dữ liệu phải được bảo toàn", "VPN", DateTime.UtcNow);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();
        await context.GetService<IMigrator>().MigrateAsync("20261128074418_AddDurableAiTriage");
        var legacy = Guid.NewGuid();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.ai_analyses (id, ticket_id, ticket_revision, input_hash, created_at, succeeded, failure_reason, decision)
            VALUES ({legacy}, {ticket.Id}, 1, {new string('a', 64)}, {DateTime.UtcNow}, false, 'unavailable', 'Pending')
            """);
        await context.Database.MigrateAsync();
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal("Trước migration", (await context.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticket.Id)).Title);
        Assert.Equal(TriageOutcome.Unavailable, (await context.AiAnalyses.SingleAsync(a => a.Id == legacy)).Outcome);
    }
}
