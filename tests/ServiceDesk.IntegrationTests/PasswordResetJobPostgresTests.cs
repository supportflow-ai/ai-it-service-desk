using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using ServiceDesk.Modules.Identity;
using ServiceDesk.Modules.Identity.Application.ForgotPassword;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Domain;
using ServiceDesk.Modules.Identity.Infrastructure;
using ServiceDesk.Platform;
using ServiceDesk.SharedKernel.Application.Abstractions;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class PasswordResetJobPostgresTests(TicketIdempotencyPostgresFixture fixture) : IClassFixture<TicketIdempotencyPostgresFixture>
{
    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = DateTimeOffset.UtcNow;
        public void Advance(TimeSpan time) => UtcNow += time;
    }

    private sealed class Sender : IOtpSender
    {
        internal ConcurrentQueue<string> Codes { get; } = new();
        internal Exception? Failure { get; set; }
        internal Action? BeforeFailure { get; set; }
        public Task SendPasswordResetCodeAsync(string email, string code, DateTime expiresAt, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Codes.Enqueue(code);
            BeforeFailure?.Invoke();
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }

    private ServiceProvider Provider(Clock clock, IOtpSender sender, RecordingLoggerProvider? logs = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = fixture.ConnectionString,
            ["Otp:Key"] = OtpTestSettings.Key
        }).Build();
        var services = new ServiceCollection().AddLogging();
        if (logs is not null) services.AddLogging(logging => logging.AddProvider(logs));
        services.AddPersistence(config).AddIdentityStores().AddIdentityModule(config);
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton<IClock>(clock);
        services.AddSingleton(sender);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private async Task ResetAsync()
    {
        // Mỗi class có container dùng một lần riêng; không dọn DB ngoài fixture.
        await using var context = fixture.CreateContext();
        await context.PasswordResetJobs.ExecuteDeleteAsync();
        await context.OtpCodes.ExecuteDeleteAsync();
    }

    private static async Task<string> UserAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var email = $"delivery-{Guid.NewGuid():N}@test.invalid";
        var result = await scope.ServiceProvider.GetRequiredService<IUserAccountGateway>().CreateRequesterAsync(email, "Test@1234", null, default);
        Assert.NotNull(result.User);
        return email;
    }

    private static async Task<bool> EnqueueAsync(IServiceProvider provider, string email)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPasswordResetQueue>().EnqueueAsync(email);
    }

    private static async Task<PasswordResetJob?> ClaimAsync(IServiceProvider provider, Clock clock)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPasswordResetJobStore>().ClaimAsync(clock.UtcNow.UtcDateTime);
    }

    private static async Task ProcessAsync(IServiceProvider provider, PasswordResetJob claim)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PasswordResetCodeIssuer>().ProcessAsync(claim);
    }

    [Fact]
    public async Task ConcurrentClaimsAndExpiredLeaseFenceTheOldWorker()
    {
        await ResetAsync();
        var clock = new Clock();
        await using var provider = Provider(clock, new Sender());
        Assert.True(await EnqueueAsync(provider, "claim@test.invalid"));
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = Enumerable.Range(0, 12).Select(async _ => { await gate.Task; return await ClaimAsync(provider, clock); }).ToArray();
        gate.SetResult();
        var claimed = Assert.Single(await Task.WhenAll(requests), j => j is not null)!;
        clock.Advance(TimeSpan.FromMinutes(1));
        var next = Assert.IsType<PasswordResetJob>(await ClaimAsync(provider, clock));
        Assert.Equal(claimed.Id, next.Id);
        Assert.NotEqual(claimed.LeaseId, next.LeaseId);
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPasswordResetJobStore>();
        await store.CompleteAsync(claimed, clock.UtcNow.UtcDateTime);
        await store.FailAsync(claimed, clock.UtcNow.UtcDateTime, true, "stale-worker");
        await using var context = fixture.CreateContext();
        var saved = await context.PasswordResetJobs.SingleAsync();
        Assert.Null(saved.CompletedAt);
        Assert.Equal(next.LeaseId, saved.LeaseId);
        Assert.Null(saved.FailureCode);
    }

    [Fact]
    public async Task FullQueueAdmissionIsAtomicAndHttpReturns503ForEveryEmail()
    {
        await ResetAsync();
        var clock = new Clock();
        await using var provider = Provider(clock, new Sender());
        await using (var context = fixture.CreateContext())
        {
            context.PasswordResetJobs.AddRange(Enumerable.Range(0, PasswordResetJob.Capacity - 1)
                .Select(i => PasswordResetJob.Request($"capacity-{i}@test.invalid", clock.UtcNow.UtcDateTime)));
            await context.SaveChangesAsync();
        }
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = Enumerable.Range(0, 16).Select(async i => { await gate.Task; return await EnqueueAsync(provider, $"racing-{i}@test.invalid"); }).ToArray();
        gate.SetResult();
        Assert.Single(await Task.WhenAll(requests), admitted => admitted);
        await using var host = fixture.First!.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IHostedService) &&
                         d.ImplementationType?.Name == "PasswordResetQueueProcessor").ToArray()) services.Remove(descriptor);
        }));
        using var client = host.CreateClient();
        var known = await UserAsync(provider);
        using var one = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = known });
        using var two = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "unknown@test.invalid" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, one.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, two.StatusCode);
        Assert.Equal(await one.Content.ReadAsStringAsync(), await two.Content.ReadAsStringAsync());
        await using var check = fixture.CreateContext();
        Assert.Equal(PasswordResetJob.Capacity, await check.PasswordResetJobs.CountAsync());
    }

    [Fact]
    public async Task CrashAfterPreparingOrAfterSmtpAcceptanceRetriesTheSameCodeAcrossProviders()
    {
        await ResetAsync();
        var clock = new Clock();
        var sender = new Sender { Failure = new IOException("simulated-crash-after-SMTP-acceptance") };
        string email;
        Guid codeId;
        await using (var provider = Provider(clock, sender))
        {
            email = await UserAsync(provider);
            Assert.True(await EnqueueAsync(provider, email));
            var claim = Assert.IsType<PasswordResetJob>(await ClaimAsync(provider, clock));
            await Assert.ThrowsAsync<IOException>(() => ProcessAsync(provider, claim));
            await using var context = fixture.CreateContext();
            var prepared = await context.PasswordResetJobs.SingleAsync();
            Assert.NotNull(prepared.ProtectedCode);
            Assert.DoesNotContain(Assert.Single(sender.Codes), prepared.ProtectedCode);
            codeId = prepared.OtpCodeId!.Value;
        }
        clock.Advance(TimeSpan.FromMinutes(1));
        sender.Failure = null;
        await using (var recreated = Provider(clock, sender))
        {
            var claim = Assert.IsType<PasswordResetJob>(await ClaimAsync(recreated, clock));
            Assert.Equal(codeId, claim.OtpCodeId);
            await ProcessAsync(recreated, claim);
        }
        Assert.Equal(2, sender.Codes.Count);
        Assert.Single(sender.Codes.Distinct());
        await using var check = fixture.CreateContext();
        Assert.Equal(1, await check.OtpCodes.CountAsync(o => o.Target == email));
        var finished = await check.PasswordResetJobs.SingleAsync();
        Assert.NotNull(finished.CompletedAt);
        Assert.Null(finished.ProtectedCode);
        Assert.Null(finished.FailureCode);
    }

    [Theory]
    [InlineData("consumed")]
    [InlineData("expired")]
    [InlineData("exhausted")]
    public async Task InvalidatedOrExpiredPreparedCodeIsNeverSent(string invalidation)
    {
        await ResetAsync();
        var clock = new Clock();
        var sender = new Sender { Failure = new IOException("stop-after-prepare") };
        await using var provider = Provider(clock, sender);
        var email = await UserAsync(provider);
        Assert.True(await EnqueueAsync(provider, email));
        var first = Assert.IsType<PasswordResetJob>(await ClaimAsync(provider, clock));
        await Assert.ThrowsAsync<IOException>(() => ProcessAsync(provider, first));
        if (invalidation == "expired") clock.Advance(OtpCode.Lifetime);
        else
        {
            await using var context = fixture.CreateContext();
            if (invalidation == "consumed")
                await context.OtpCodes.Where(o => o.Target == email).ExecuteUpdateAsync(s => s.SetProperty(o => o.IsUsed, true));
            else
                await context.OtpCodes.Where(o => o.Target == email).ExecuteUpdateAsync(s => s.SetProperty(o => o.FailedAttempts, OtpCode.MaxFailedAttempts));
            clock.Advance(TimeSpan.FromMinutes(1));
        }
        sender.Failure = null;
        var retry = await ClaimAsync(provider, clock);
        if (retry is not null) await ProcessAsync(provider, retry);
        Assert.Single(sender.Codes);
    }

    [Fact]
    public async Task TransientFailureUsesThePreparedExpiryAfterARequestWaitedInTheQueue()
    {
        await ResetAsync();
        var clock = new Clock();
        var sender = new Sender { Failure = new SmtpException(SmtpStatusCode.MailboxBusy, "transient") };
        sender.BeforeFailure = () => clock.Advance(TimeSpan.FromSeconds(20));
        await using var provider = Provider(clock, sender);
        var email = await UserAsync(provider);
        Assert.True(await EnqueueAsync(provider, email));
        clock.Advance(TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(50));
        var claim = Assert.IsType<PasswordResetJob>(await ClaimAsync(provider, clock));
        await Assert.ThrowsAsync<SmtpException>(() => ProcessAsync(provider, claim));
        await using (var scope = provider.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IPasswordResetJobStore>().FailAsync(claim, clock.UtcNow.UtcDateTime, true, "TransientDeliveryFailure");
        await using (var context = fixture.CreateContext())
        {
            var job = await context.PasswordResetJobs.SingleAsync();
            Assert.True(job.ExpiresAt > clock.UtcNow.UtcDateTime);
            Assert.Null(job.CompletedAt);
            Assert.NotNull(job.ProtectedCode);
            Assert.Equal(clock.UtcNow.UtcDateTime.AddSeconds(5).Ticks / 10, job.NextAttemptAt.Ticks / 10);
        }
        clock.Advance(TimeSpan.FromSeconds(5));
        sender.BeforeFailure = null;
        sender.Failure = null;
        await ProcessAsync(provider, Assert.IsType<PasswordResetJob>(await ClaimAsync(provider, clock)));
        Assert.Equal(2, sender.Codes.Count);
        Assert.Single(sender.Codes.Distinct());
    }

    private sealed class ExpiringProtector(IOtpCodeProtector inner, Clock clock) : IOtpCodeProtector
    {
        public string Hash(string email, string purpose, string code) => inner.Hash(email, purpose, code);
        public string Protect(string email, Guid otpId, string code) => inner.Protect(email, otpId, code);
        public string Unprotect(string email, Guid otpId, string cipher)
        {
            var code = inner.Unprotect(email, otpId, cipher);
            clock.Advance(OtpCode.Lifetime + TimeSpan.FromSeconds(1));
            return code;
        }
    }

    [Fact]
    public async Task ExpiryBetweenLeaseCheckAndSmtpStartPreventsSending()
    {
        await ResetAsync();
        var clock = new Clock();
        var sender = new Sender();
        await using var provider = Provider(clock, sender);
        var email = await UserAsync(provider);
        Assert.True(await EnqueueAsync(provider, email));
        var claim = Assert.IsType<PasswordResetJob>(await ClaimAsync(provider, clock));
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var issuer = new PasswordResetCodeIssuer(services.GetRequiredService<IUserAccountGateway>(), services.GetRequiredService<IPasswordResetJobStore>(),
            sender, clock, new ExpiringProtector(services.GetRequiredService<IOtpCodeProtector>(), clock));
        await issuer.ProcessAsync(claim);
        Assert.Empty(sender.Codes);
    }

    [Fact]
    public async Task RetryScheduleAndAttemptsAreBoundedAndUnknownEmailCreatesNoCode()
    {
        await ResetAsync();
        var clock = new Clock();
        await using var provider = Provider(clock, new Sender());
        Assert.True(await EnqueueAsync(provider, "nobody@test.invalid"));
        var unknown = Assert.IsType<PasswordResetJob>(await ClaimAsync(provider, clock));
        await ProcessAsync(provider, unknown);
        Assert.True(await EnqueueAsync(provider, await UserAsync(provider)));
        for (var i = 1; i <= PasswordResetJob.MaxAttempts; i++)
        {
            var claim = Assert.IsType<PasswordResetJob>(await ClaimAsync(provider, clock));
            Assert.Equal(i, claim.Attempts);
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IPasswordResetJobStore>().FailAsync(claim, clock.UtcNow.UtcDateTime, true, "TransientDeliveryFailure");
            Assert.Null(await ClaimAsync(provider, clock));
            clock.Advance(TimeSpan.FromSeconds(Math.Min(60, 5 * (1 << (i - 1)))));
        }
        Assert.Null(await ClaimAsync(provider, clock));
        await using var context = fixture.CreateContext();
        Assert.Equal(2, await context.PasswordResetJobs.CountAsync(j => j.CompletedAt != null));
        Assert.Empty(await context.OtpCodes.ToListAsync());
    }

    private static async Task UntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(25);
        }
        Assert.Fail("Worker không đạt điều kiện trong thời gian chờ hữu hạn.");
    }

    [Theory]
    [InlineData("permanent")]
    [InlineData("transient")]
    [InlineData("timeout")]
    [InlineData("missing")]
    public async Task WorkerClassifiesSmtpFailureAndRetriesWithoutLeakingSecrets(string failure)
    {
        await ResetAsync();
        var clock = new Clock();
        using var logs = new RecordingLoggerProvider();
        var sender = new Sender();
        var permanent = failure is "permanent" or "missing";
        IOtpSender delivery = failure == "missing"
            ? new NoOpOtpSender(Microsoft.Extensions.Logging.Abstractions.NullLogger<NoOpOtpSender>.Instance)
            : sender;
        await using var provider = Provider(clock, delivery, logs);
        var email = await UserAsync(provider);
        sender.Failure = failure == "timeout" ? new OperationCanceledException("SMTP timeout") :
            new SmtpException(permanent ? SmtpStatusCode.MailboxUnavailable : SmtpStatusCode.MailboxBusy, $"private {email} {OtpTestSettings.Key}");
        Assert.True(await EnqueueAsync(provider, email));
        using (var worker = ActivatorUtilities.CreateInstance<PasswordResetQueueProcessor>(provider))
        {
            await worker.StartAsync(CancellationToken.None);
            try
            {
                await UntilAsync(async () =>
                {
                    await using var context = fixture.CreateContext();
                    return await context.PasswordResetJobs.AnyAsync(j => j.FailureCode != null);
                });
            }
            finally
            {
                using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await worker.StopAsync(stop.Token);
            }
        }
        await using (var context = fixture.CreateContext())
        {
            var failed = await context.PasswordResetJobs.SingleAsync();
            Assert.Equal(permanent, failed.CompletedAt is not null);
            Assert.Equal(permanent ? "PermanentDeliveryFailure" : "TransientDeliveryFailure", failed.FailureCode);
            Assert.Equal(1, failed.Attempts);
        }
        if (!permanent)
        {
            clock.Advance(TimeSpan.FromSeconds(5));
            sender.Failure = null;
            await ProcessAsync(provider, Assert.IsType<PasswordResetJob>(await ClaimAsync(provider, clock)));
            Assert.Equal(2, sender.Codes.Count);
            Assert.Single(sender.Codes.Distinct());
        }
        var text = string.Join("\n", logs.Messages);
        Assert.NotEmpty(text);
        foreach (var secret in sender.Codes.Append(email).Append(OtpTestSettings.Key)) Assert.DoesNotContain(secret, text);
    }

    [Fact]
    public async Task FailedOutboxWriteRollsBackTheNewCodeAndInvalidationOfTheOldCode()
    {
        await ResetAsync();
        var clock = new Clock();
        await using var provider = Provider(clock, new Sender());
        var email = await UserAsync(provider);
        var now = clock.UtcNow.UtcDateTime;
        var oldCode = OtpCode.Issue(email, OtpCode.ResetPasswordPurpose, "old-code-digest", now);
        await using (var scope = provider.CreateAsyncScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<IOtpCodeStore>().TryIssueAsync(oldCode, 5, now.AddHours(-1)));
        Assert.True(await EnqueueAsync(provider, email));
        var claim = Assert.IsType<PasswordResetJob>(await ClaimAsync(provider, clock));
        var name = "fail_outbox_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var install = new NpgsqlCommand($"""
            CREATE FUNCTION public.{name}() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.id = '{claim.Id}'::uuid AND NEW.protected_code IS NOT NULL THEN RAISE EXCEPTION 'injected-outbox-failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER {name} BEFORE UPDATE ON public.password_reset_jobs FOR EACH ROW EXECUTE FUNCTION public.{name}();
            """, connection);
        await install.ExecuteNonQueryAsync();
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => ProcessAsync(provider, claim));
            await using var context = fixture.CreateContext();
            Assert.False((await context.OtpCodes.SingleAsync(o => o.Id == oldCode.Id)).IsUsed);
            Assert.Equal(1, await context.OtpCodes.CountAsync());
            var job = await context.PasswordResetJobs.SingleAsync();
            Assert.Null(job.ProtectedCode);
            Assert.Null(job.OtpCodeId);
        }
        finally
        {
            await using var remove = new NpgsqlCommand($"DROP TRIGGER {name} ON public.password_reset_jobs; DROP FUNCTION public.{name}();", connection);
            await remove.ExecuteNonQueryAsync();
        }
        await ProcessAsync(provider, claim);
        await using var check = fixture.CreateContext();
        Assert.True((await check.OtpCodes.SingleAsync(o => o.Id == oldCode.Id)).IsUsed);
        Assert.Equal(2, await check.OtpCodes.CountAsync());
        Assert.NotNull((await check.PasswordResetJobs.SingleAsync()).CompletedAt);
    }
}
