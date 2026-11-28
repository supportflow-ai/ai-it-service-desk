using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Domain;
using ServiceDesk.Modules.Identity.Infrastructure;
using ServiceDesk.Modules.Identity.Infrastructure.Options;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class OtpVerificationPostgresTests(TicketIdempotencyPostgresFixture fixture) : IClassFixture<TicketIdempotencyPostgresFixture>
{
    [Theory]
    [InlineData("valid")]
    [InlineData("wrong-account")]
    [InlineData("wrong-purpose")]
    [InlineData("old-digest")]
    [InlineData("other-key")]
    [InlineData("expired")]
    public async Task HttpResetRequiresTheCurrentKeyAccountPurposeAndLiveCode(string mode)
    {
        const string code = "123456";
        const string originalPassword = "Test@1234";
        const string newPassword = "Changed@1234";
        using var client = fixture.Second!.CreateClient();
        await using var scope = fixture.First!.Services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IUserAccountGateway>();
        var user = (await accounts.CreateRequesterAsync($"otp-{Guid.NewGuid():N}@test.invalid", originalPassword, null, default)).User!;
        var protector = scope.ServiceProvider.GetRequiredService<IOtpCodeProtector>();
        var hash = protector.Hash(user.Email, mode == "wrong-purpose" ? "OtherPurpose" : OtpCode.ResetPasswordPurpose, code);
        if (mode == "old-digest") hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
        if (mode == "other-key") hash = new OtpCodeProtector(Options.Create(new OtpProtectionOptions
        {
            Key = Convert.ToBase64String(Enumerable.Repeat((byte)9, 32).ToArray())
        })).Hash(user.Email, OtpCode.ResetPasswordPurpose, code);
        var now = DateTime.UtcNow;
        var otp = OtpCode.Issue(user.Email, OtpCode.ResetPasswordPurpose, hash,
            mode == "expired" ? now.Subtract(OtpCode.Lifetime).AddSeconds(-1) : now);
        Assert.True(await scope.ServiceProvider.GetRequiredService<IOtpCodeStore>().TryIssueAsync(otp, 5, now.AddHours(-1)));
        var email = user.Email;
        if (mode == "wrong-account") email = (await accounts.CreateRequesterAsync($"other-{Guid.NewGuid():N}@test.invalid", originalPassword, null, default)).User!.Email;

        using var response = await client.PostAsJsonAsync("/api/auth/reset-password", new { email, otpCode = code, newPassword });

        Assert.Equal(mode == "valid" ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);
        await using var verify = fixture.First.Services.CreateAsyncScope();
        var users = verify.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True(await users.CheckPasswordAsync((await users.FindByIdAsync(user.UserId))!, mode == "valid" ? newPassword : originalPassword));
        if (mode == "valid")
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/reset-password", new { email, otpCode = code, newPassword = "Again@1234" })).StatusCode);
        await using var context = fixture.CreateContext();
        Assert.Equal(mode == "valid", (await context.OtpCodes.SingleAsync(o => o.Id == otp.Id)).IsUsed);
    }
    private async Task<(UserInfo User, OtpCode Code)> SeedAsync(string code)
    {
        await using var scope = fixture.First!.Services.CreateAsyncScope();
        var user = (await scope.ServiceProvider.GetRequiredService<IUserAccountGateway>()
            .CreateRequesterAsync($"reset-{Guid.NewGuid():N}@test.invalid", "Test@1234", null, default)).User!;
        var hash = scope.ServiceProvider.GetRequiredService<IOtpCodeProtector>().Hash(user.Email, OtpCode.ResetPasswordPurpose, code);
        var otp = OtpCode.Issue(user.Email, OtpCode.ResetPasswordPurpose, hash, DateTime.UtcNow);
        Assert.True(await scope.ServiceProvider.GetRequiredService<IOtpCodeStore>().TryIssueAsync(otp, 5, DateTime.UtcNow.AddHours(-1)));
        return (user, otp);
    }

    [Fact]
    public async Task ConcurrentHttpResetAcrossHostsConsumesTheCodeExactlyOnce()
    {
        var (user, otp) = await SeedAsync("739251");
        using var first = fixture.First!.CreateClient();
        using var second = fixture.Second!.CreateClient();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> ResetAsync(HttpClient client)
        {
            await start.Task;
            return await client.PostAsJsonAsync("/api/auth/reset-password", new { email = user.Email, otpCode = "739251", newPassword = "Changed@1234" });
        }
        var one = ResetAsync(first);
        var two = ResetAsync(second);
        start.SetResult();
        using var firstResult = await one;
        using var secondResult = await two;
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest }.Order(), new[] { firstResult.StatusCode, secondResult.StatusCode }.Order());
        await using var context = fixture.CreateContext();
        Assert.True((await context.OtpCodes.SingleAsync(o => o.Id == otp.Id)).IsUsed);
    }

    [Fact]
    public async Task FailureAfterPasswordWriteRollsBackPasswordStampOtpAndSessions()
    {
        var (user, otp) = await SeedAsync("739251");
        using var client = fixture.First!.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "Test@1234" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await using var context = fixture.CreateContext();
        var before = await context.Users.AsNoTracking().SingleAsync(u => u.Id == user.UserId);
        var name = "fail_reset_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var installFailure = new NpgsqlCommand($"""
            CREATE FUNCTION public.{name}() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.id = '{otp.Id}'::uuid AND NEW.is_used THEN RAISE EXCEPTION 'injected-reset-failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER {name} BEFORE UPDATE ON public.otp_codes FOR EACH ROW EXECUTE FUNCTION public.{name}();
            """, connection);
        await installFailure.ExecuteNonQueryAsync();
        try
        {
            using var failed = await client.PostAsJsonAsync("/api/auth/reset-password", new { email = user.Email, otpCode = "739251", newPassword = "Changed@1234" });
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            Assert.DoesNotContain("injected-reset-failure", await failed.Content.ReadAsStringAsync());
            var after = await context.Users.AsNoTracking().SingleAsync(u => u.Id == user.UserId);
            Assert.Equal(before.PasswordHash, after.PasswordHash);
            Assert.Equal(before.SecurityStamp, after.SecurityStamp);
            var unchanged = await context.OtpCodes.AsNoTracking().SingleAsync(o => o.Id == otp.Id);
            Assert.False(unchanged.IsUsed);
            Assert.Equal(0, unchanged.FailedAttempts);
            Assert.Equal(1, await context.RefreshTokens.CountAsync(t => t.UserId == user.UserId && t.RevokedAt == null));
        }
        finally
        {
            await using var removeFailure = new NpgsqlCommand($"DROP TRIGGER {name} ON public.otp_codes; DROP FUNCTION public.{name}();", connection);
            await removeFailure.ExecuteNonQueryAsync();
        }
        using var retry = await client.PostAsJsonAsync("/api/auth/reset-password", new { email = user.Email, otpCode = "739251", newPassword = "Changed@1234" });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.False(await context.RefreshTokens.AnyAsync(t => t.UserId == user.UserId && t.RevokedAt == null));
    }

    [Fact]
    public async Task HttpFailureLogsContainNeitherOtpEmailNorProtectionKey()
    {
        var (user, _) = await SeedAsync("739251");
        using var logs = new RecordingLoggerProvider();
        await using var host = fixture.First!.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = host.CreateClient();
        using var failed = await client.PostAsJsonAsync("/api/auth/reset-password", new { email = user.Email, otpCode = "924853", newPassword = "Changed@1234" });
        Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);
        Assert.NotEmpty(logs.Messages);
        var text = string.Join("\n", logs.Messages);
        foreach (var secret in new[] { user.Email, "739251", "924853", OtpTestSettings.Key, "Changed@1234" })
            Assert.DoesNotContain(secret, text);
    }
}
