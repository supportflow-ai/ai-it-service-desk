using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ServiceDesk.Modules.Identity.Presentation;
using ServiceDesk.SharedKernel.Application.Abstractions;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Unit")]
public sealed class AuthEmailRateLimiterTests
{
    private static ServiceProvider Services(TestClock clock, int permits = 1, bool enabled = true) => new ServiceCollection()
        .AddSingleton<IClock>(clock)
        .AddSingleton<IOptions<AuthRateLimitOptions>>(Options.Create(new AuthRateLimitOptions
        {
            Enabled = enabled, EmailPermitLimit = permits, EmailWindowSeconds = 10
        }))
        .AddSingleton<AuthEmailRateLimiter>()
        .BuildServiceProvider();

    private static void Fill(AuthEmailRateLimiter limiter, int count)
    {
        for (var i = 0; i < count; i++)
            Assert.True(limiter.TryAcquire(AuthEmailOperation.Login, $"filled-{i}@test.invalid", out _));
    }

    [Fact]
    public void FullCapacityRejectsNewEmailsAndPreservesEveryExistingBudget()
    {
        using var services = Services(new TestClock());
        var limiter = services.GetRequiredService<AuthEmailRateLimiter>();
        Fill(limiter, AuthEmailRateLimiter.MaxPartitions);

        for (var i = 0; i < 100; i++)
        {
            Assert.False(limiter.TryAcquire(AuthEmailOperation.Login, $"new-{i}@test.invalid", out var retry));
            Assert.Equal(TimeSpan.FromSeconds(10), retry);
        }
        for (var i = 0; i < AuthEmailRateLimiter.MaxPartitions; i++)
            Assert.False(limiter.TryAcquire(AuthEmailOperation.Login, $"filled-{i}@test.invalid", out _));
    }

    private static async Task<bool[]> Race(IEnumerable<Func<bool>> actions)
    {
        var work = actions.ToArray();
        using var ready = new CountdownEvent(work.Length);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = work.Select(action => Task.Run(async () =>
        {
            ready.Signal();
            await start.Task;
            return action();
        })).ToArray();
        var allReady = ready.Wait(TimeSpan.FromSeconds(10));
        start.SetResult();
        var results = await Task.WhenAll(tasks);
        Assert.True(allReady);
        return results;
    }

    [Fact]
    public async Task ConcurrentAdmissionCannotExceedTheRemainingSlot()
    {
        using var services = Services(new TestClock());
        var limiter = services.GetRequiredService<AuthEmailRateLimiter>();
        Fill(limiter, AuthEmailRateLimiter.MaxPartitions - 1);

        var admitted = await Race(Enumerable.Range(0, 64).Select(i => (Func<bool>)(() =>
            limiter.TryAcquire(AuthEmailOperation.Login, $"racing-{i}@test.invalid", out _))));

        Assert.Single(admitted, acquired => acquired);
        Assert.False(limiter.TryAcquire(AuthEmailOperation.Login, "filled-0@test.invalid", out _));
        Assert.False(limiter.TryAcquire(AuthEmailOperation.Login, "another@test.invalid", out _));
    }

    [Fact]
    public void ExpiryFreesOnlyExpiredPartitionsAndReturnsTheEarliestRetry()
    {
        var clock = new TestClock();
        using var services = Services(clock);
        var limiter = services.GetRequiredService<AuthEmailRateLimiter>();
        Assert.True(limiter.TryAcquire(AuthEmailOperation.Login, "oldest@test.invalid", out _));
        clock.Advance(TimeSpan.FromSeconds(3));
        Fill(limiter, AuthEmailRateLimiter.MaxPartitions - 1);
        clock.Advance(TimeSpan.FromSeconds(4));

        Assert.False(limiter.TryAcquire(AuthEmailOperation.Login, "new@test.invalid", out var retry));
        Assert.Equal(TimeSpan.FromSeconds(3), retry);
        clock.Advance(retry);
        Assert.True(limiter.TryAcquire(AuthEmailOperation.Login, "new@test.invalid", out _));
        Assert.False(limiter.TryAcquire(AuthEmailOperation.Login, "filled-0@test.invalid", out retry));
        Assert.Equal(TimeSpan.FromSeconds(3), retry);
        Assert.False(limiter.TryAcquire(AuthEmailOperation.Login, "another@test.invalid", out retry));
        Assert.Equal(TimeSpan.FromSeconds(3), retry);
    }

    [Fact]
    public async Task RenewalSurvivesConcurrentExpiryEvictionAndAcquisition()
    {
        var clock = new TestClock();
        using var services = Services(clock);
        var limiter = services.GetRequiredService<AuthEmailRateLimiter>();
        for (var round = 0; round < 5; round++)
        {
            Fill(limiter, AuthEmailRateLimiter.MaxPartitions);
            clock.Advance(TimeSpan.FromSeconds(10));
            var renewed = await Race(Enumerable.Range(0, 64).Select(i => (Func<bool>)(() =>
                limiter.TryAcquire(AuthEmailOperation.Login, i < 32 ? "filled-0@test.invalid" : $"new-{i}@test.invalid", out _))));
            Assert.Single(renewed.Take(32), acquired => acquired);
            Assert.False(limiter.TryAcquire(AuthEmailOperation.Login, "filled-0@test.invalid", out var retry));
            Assert.Equal(TimeSpan.FromSeconds(10), retry);
            clock.Advance(TimeSpan.FromSeconds(10));
        }
    }

    [Fact]
    public void NormalizationKeepsOperationBudgetsIndependentAndResetsAtExpiry()
    {
        var clock = new TestClock();
        using var services = Services(clock);
        var limiter = services.GetRequiredService<AuthEmailRateLimiter>();
        foreach (var operation in Enum.GetValues<AuthEmailOperation>())
        {
            Assert.True(limiter.TryAcquire(operation, " Person@Test.Invalid ", out _));
            Assert.False(limiter.TryAcquire(operation, "person@test.invalid", out var retry));
            Assert.Equal(TimeSpan.FromSeconds(10), retry);
        }
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.True(limiter.TryAcquire(AuthEmailOperation.Login, "PERSON@test.invalid", out _));
        Assert.False(limiter.TryAcquire(AuthEmailOperation.Login, "person@test.invalid", out _));
    }

    [Fact]
    public void OversizedEmailIsRejectedWithoutOccupyingOrClearingPartitions()
    {
        using var services = Services(new TestClock());
        var limiter = services.GetRequiredService<AuthEmailRateLimiter>();
        Assert.False(limiter.TryAcquire(AuthEmailOperation.Login, new string('a', 100_000), out var retry));
        Assert.Equal(TimeSpan.FromSeconds(10), retry);
        Assert.True(limiter.TryAcquire(AuthEmailOperation.Login, " " + new string('a', 320) + " ", out _));
        Assert.False(limiter.TryAcquire(AuthEmailOperation.Login, new string('A', 320), out _));
        Fill(limiter, AuthEmailRateLimiter.MaxPartitions - 1);
        Assert.False(limiter.TryAcquire(AuthEmailOperation.Login, "new@test.invalid", out _));
    }

    [Fact]
    public void InstancesHaveSeparateBudgets()
    {
        using var first = Services(new TestClock());
        using var second = Services(new TestClock());
        Assert.True(first.GetRequiredService<AuthEmailRateLimiter>().TryAcquire(AuthEmailOperation.Login, "same@test.invalid", out _));
        Assert.False(first.GetRequiredService<AuthEmailRateLimiter>().TryAcquire(AuthEmailOperation.Login, "same@test.invalid", out _));
        Assert.True(second.GetRequiredService<AuthEmailRateLimiter>().TryAcquire(AuthEmailOperation.Login, "same@test.invalid", out _));
    }

    [Fact]
    public void DisabledOrEmptyEmailDoesNotConsumePartitions()
    {
        using var enabled = Services(new TestClock());
        foreach (var email in new string?[] { null, "", "   " })
            Assert.True(enabled.GetRequiredService<AuthEmailRateLimiter>().TryAcquire(AuthEmailOperation.Login, email, out _));
        Fill(enabled.GetRequiredService<AuthEmailRateLimiter>(), AuthEmailRateLimiter.MaxPartitions);
        using var disabled = Services(new TestClock(), enabled: false);
        for (var i = 0; i <= AuthEmailRateLimiter.MaxPartitions; i++)
            Assert.True(disabled.GetRequiredService<AuthEmailRateLimiter>().TryAcquire(AuthEmailOperation.Login, $"{i}@test.invalid", out _));
    }

    private sealed class TestClock : IClock
    {
        private long ticks = new DateTimeOffset(2026, 11, 28, 0, 0, 0, TimeSpan.Zero).Ticks;
        public DateTimeOffset UtcNow => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
        public void Advance(TimeSpan elapsed) => Interlocked.Add(ref ticks, elapsed.Ticks);
    }
}
