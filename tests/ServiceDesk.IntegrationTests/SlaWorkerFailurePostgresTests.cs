using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.EvaluateSla;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.Modules.Ticketing.Infrastructure;
using ServiceDesk.Platform.Persistence;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Worker;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class SlaWorkerFailurePostgresTests(TicketIdempotencyPostgresFixture fixture) : IClassFixture<TicketIdempotencyPostgresFixture>
{
    [Fact]
    public async Task ARejectedRowDoesNotHideSuccessfulUpdatesOrBlockTheRemainingBatch()
    {
        var now = DateTime.UtcNow;
        await using (var setup = fixture.CreateContext())
        {
            foreach (var title in new[] { "SLA good A", "SLA_POISON", "SLA good B" })
            {
                var ticket = Ticket.Create(Guid.NewGuid(), title, "Partial SLA batch regression", TicketCategories.NET, now.AddHours(-10));
                ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), now.AddHours(-10));
                setup.Tickets.Add(ticket);
            }
            await setup.SaveChangesAsync();
            await setup.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION g405_reject_row() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN IF NEW.title = 'SLA_POISON' THEN RAISE EXCEPTION 'synthetic row failure'; END IF; RETURN NEW; END $$;
                CREATE TRIGGER g405_poison BEFORE UPDATE ON tickets FOR EACH ROW EXECUTE FUNCTION g405_reject_row();
                """);
        }
        var services = new ServiceCollection().AddLogging().AddSingleton<IClock>(new FrozenClock(now));
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(fixture.ConnectionString));
        services.AddScoped<ISlaEvaluationGateway>(sp => new PostgresSlaEvaluationAdapter(sp.GetRequiredService<ApplicationDbContext>(), sp.GetRequiredService<ILogger<PostgresSlaEvaluationAdapter>>()));
        services.AddScoped<IEvaluateSla>(sp => new EvaluateSlaUseCase(sp.GetRequiredService<ISlaEvaluationGateway>(), sp.GetRequiredService<IClock>()));
        await using var provider = services.BuildServiceProvider();
        using var worker = new SlaEvaluationWorker(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<SlaEvaluationWorker>.Instance);
        try
        {
            var result = await worker.RunEvaluationSweepAsync(CancellationToken.None);
            Assert.Equal(3, result.ProcessedCount);
            Assert.Equal(2, result.UpdatedCount);
            Assert.Equal(1, result.FailedCount);
            Assert.Equal("PartialFailure", result.Outcome);
            Assert.Equal("PartialFailure", worker.Snapshot.State);
            Assert.Null(worker.Snapshot.LastSuccessfulAt);
            await using var verify = fixture.CreateContext();
            Assert.Equal(2, await verify.Tickets.CountAsync(t => t.FirstResponseBreached));
        }
        finally
        {
            await using var cleanup = fixture.CreateContext();
            await cleanup.Database.ExecuteSqlRawAsync("DROP TRIGGER g405_poison ON tickets; DROP FUNCTION g405_reject_row();");
        }
        using var restarted = new SlaEvaluationWorker(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<SlaEvaluationWorker>.Instance);
        var recovered = await restarted.RunEvaluationSweepAsync(CancellationToken.None);
        Assert.Equal(1, recovered.UpdatedCount);
        Assert.Equal(0, recovered.FailedCount);
        Assert.Equal("Success", restarted.Snapshot.State);
        await using var recoveredDb = fixture.CreateContext();
        Assert.Equal(3, await recoveredDb.Tickets.CountAsync(t => t.FirstResponseBreached));
    }

    private sealed class FrozenClock(DateTime now) : IClock
    {
        public DateTimeOffset UtcNow => new(now);
    }
}
