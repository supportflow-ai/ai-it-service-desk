using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceDesk.Application.Identity.Ports;
using ServiceDesk.Infrastructure;
using ServiceDesk.Infrastructure.Persistence;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class IdentityConcurrencyTests(TicketIdempotencyPostgresFixture fixture)
    : IClassFixture<TicketIdempotencyPostgresFixture>
{
    private const string Password = "Test@1234";

    private async Task<string> RegisterAsync()
    {
        var email = $"concurrent-{Guid.NewGuid()}@test.invalid";
        using var client = fixture.First!.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return email;
    }

    [Fact]
    public async Task FiveConcurrentFailuresAcrossTwoHostsLockAccount()
    {
        var email = await RegisterAsync();
        using var first = fixture.First!.CreateClient();
        using var second = fixture.Second!.CreateClient();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = Enumerable.Range(0, 5).Select(async index =>
        {
            await start.Task;
            using var response = await (index % 2 == 0 ? first : second)
                .PostAsJsonAsync("/api/auth/login", new { email, password = "wrong" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }).ToArray();
        start.SetResult();
        await Task.WhenAll(attempts);
        await using var db = fixture.CreateContext();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        Assert.True(user.LockoutEnd > DateTimeOffset.UtcNow);
        using var locked = await first.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
    }

    [Fact]
    public async Task SecurityWriteFailureReturnsServerErrorAndRollsBack()
    {
        var email = await RegisterAsync();
        await using var db = fixture.CreateContext();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION fail_security_update() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'synthetic security write failure'; END $$;
            CREATE TRIGGER fail_security_update BEFORE UPDATE ON "AspNetUsers"
              FOR EACH ROW EXECUTE FUNCTION fail_security_update();
            """);
        try
        {
            using var client = fixture.First!.CreateClient();
            using var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong" });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.DoesNotContain("token", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, (await db.Users.SingleAsync(u => u.Email == email)).AccessFailedCount);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_security_update ON \"AspNetUsers\"; DROP FUNCTION fail_security_update();");
        }
    }

    [Fact]
    public async Task CancellationAfterSecuritySaveRollsBackAndReleasesAccountLock()
    {
        var email = await RegisterAsync();
        using var cancellation = new CancellationTokenSource();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(fixture.ConnectionString)
            .AddInterceptors(new CancelAfterSave(cancellation)));
        builder.Services.AddIdentityStores();
        using var host = builder.Build();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                scope.ServiceProvider.GetRequiredService<IUserAccountGateway>()
                    .CheckPasswordAsync(email, "wrong", cancellation.Token));
        }
        await using var db = fixture.CreateContext();
        Assert.Equal(0, (await db.Users.SingleAsync(u => u.Email == email)).AccessFailedCount);
        using var client = fixture.Second!.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LockedAccountDoesNotBlockAnotherAccount()
    {
        var firstEmail = await RegisterAsync();
        var secondEmail = await RegisterAsync();
        await using var db = fixture.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM public.\"AspNetUsers\" WHERE \"Email\" = {firstEmail} FOR UPDATE");
        using var client = fixture.Second!.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email = secondEmail, password = Password })
            .WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class CancelAfterSave(CancellationTokenSource cancellation) : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task ConcurrentSuccessAndFailureHaveAValidSerialOrder()
    {
        var email = await RegisterAsync();
        using var first = fixture.First!.CreateClient();
        using var second = fixture.Second!.CreateClient();
        using (var failed = await first.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong" }))
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Login(HttpClient client, string password)
        {
            await start.Task;
            return await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        }
        var correct = Login(first, Password);
        var incorrect = Login(second, "wrong");
        start.SetResult();
        using var successful = await correct;
        using var unsuccessful = await incorrect;
        Assert.Equal(HttpStatusCode.OK, successful.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unsuccessful.StatusCode);
        await using var db = fixture.CreateContext();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        // The success either resets both failures or precedes the last failure.
        Assert.InRange(user.AccessFailedCount, 0, 1);
        Assert.True(user.LockoutEnd is null || user.LockoutEnd <= DateTimeOffset.UtcNow);
    }
}
