using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Identity.Domain;
using ServiceDesk.Platform.Persistence;
using ServiceDesk.SharedKernel.Application.Abstractions;
using Xunit;

namespace ServiceDesk.IntegrationTests;

/// <summary>
/// Kiểm thử trên PostgreSQL thật cho các chốt bảo mật Identity: xoay vòng refresh token nguyên tử,
/// giới hạn lần nhập sai OTP, đặt lại mật khẩu trong một giao dịch và không gỡ quyền Admin cuối cùng.
/// </summary>
[Trait("Category", "Integration")]
public sealed class IdentityHardeningPostgresTests(ServiceDeskWebApplicationFactory factory) : IClassFixture<ServiceDeskWebApplicationFactory>
{
    private const string Password = "Test@1234";

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private async Task<string> CreateUserAsync(string prefix, string? fullName = null)
    {
        var email = $"{prefix}-{Guid.NewGuid():N}@test.invalid";
        await using var scope = factory.Services.CreateAsyncScope();
        var created = await scope.ServiceProvider.GetRequiredService<IUserAccountGateway>().CreateRequesterAsync(email, Password, fullName, default);
        Assert.NotNull(created.User);
        return email;
    }

    private async Task<string> UserIdAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync(email))!.Id;
    }

    private async Task IssueOtpAsync(string email, string code)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IOtpCodeStore>()
            .TryIssueAsync(OtpCode.Issue(email, OtpCode.ResetPasswordPurpose, Hash(code), DateTime.UtcNow), int.MaxValue, DateTime.UtcNow.AddHours(-1));
    }

    private async Task<bool> ResetAsync(string email, string code, string newPassword)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IPasswordResetGateway>()
            .ResetWithOtpAsync(email, Hash(code), newPassword, DateTime.UtcNow);
        return result.Succeeded;
    }

    private async Task<bool> PasswordWorksAsync(string email, string password)
    {
        // Kiểm mật khẩu trực tiếp, không đăng nhập: không tạo refresh token làm sai các phép đếm phiên.
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        return await users.CheckPasswordAsync((await users.FindByEmailAsync(email))!, password);
    }

    [Fact]
    public async Task ConcurrentRotationOfOneRefreshTokenSucceedsExactlyOnce()
    {
        var userId = await UserIdAsync(await CreateUserAsync("rotate-once"));
        var token = await SeedTokenAsync(userId);

        async Task<bool> RotateAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRefreshTokenStore>()
                .TryRotateAsync(token.Id, token.Rotate(Hash(Guid.NewGuid().ToString()), DateTime.UtcNow), DateTime.UtcNow);
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => RotateAsync()));

        Assert.Single(results, won => won);
        Assert.Equal(1, await ActiveRefreshTokensAsync(userId));
    }

    private async Task<RefreshToken> SeedTokenAsync(string userId)
    {
        var token = RefreshToken.Issue(userId, Hash(Guid.NewGuid().ToString()), DateTime.UtcNow);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync();
        return token;
    }

    [Fact]
    public async Task WrongCodesExhaustTheOtpEvenForTheCorrectCode()
    {
        var email = await CreateUserAsync("otp-attempts");
        await IssueOtpAsync(email, "123456");

        for (var attempt = 0; attempt < OtpCode.MaxFailedAttempts; attempt++)
            Assert.False(await ResetAsync(email, "000000", "Changed@1234"));

        Assert.False(await ResetAsync(email, "123456", "Changed@1234"));
        Assert.True(await PasswordWorksAsync(email, Password));
    }

    [Fact]
    public async Task CorrectCodeResetsPasswordConsumesCodeAndRevokesSessions()
    {
        var email = await CreateUserAsync("otp-success");
        var userId = await UserIdAsync(email);
        await SeedTokenAsync(userId);
        await IssueOtpAsync(email, "654321");

        Assert.True(await ResetAsync(email, "654321", "Changed@1234"));

        Assert.True(await PasswordWorksAsync(email, "Changed@1234"));
        Assert.False(await ResetAsync(email, "654321", "Again@1234"));
        await using var check = factory.Services.CreateAsyncScope();
        var context = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await context.RefreshTokens.AnyAsync(r => r.UserId == userId && r.RevokedAt == null));
    }

    [Fact]
    public async Task RejectedNewPasswordLeavesTheCodeUsable()
    {
        var email = await CreateUserAsync("otp-weak");
        await IssueOtpAsync(email, "111222");

        Assert.False(await ResetAsync(email, "111222", "weak"));
        Assert.True(await ResetAsync(email, "111222", "Changed@1234"));
    }

    [Fact]
    public async Task IssuingANewCodeInvalidatesThePreviousOne()
    {
        var email = await CreateUserAsync("otp-reissue");
        await IssueOtpAsync(email, "333444");
        await IssueOtpAsync(email, "555666");

        Assert.False(await ResetAsync(email, "333444", "Changed@1234"));
        Assert.True(await ResetAsync(email, "555666", "Changed@1234"));
    }

    [Fact]
    public async Task LastAdministratorCannotLoseTheAdminRole()
    {
        string lastAdminId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var adminEmail = await CreateUserAsync("last-admin");
            var lastAdmin = (await users.FindByEmailAsync(adminEmail))!;
            Assert.True((await users.AddToRoleAsync(lastAdmin, RoleNames.Admin)).Succeeded);
            foreach (var other in (await users.GetUsersInRoleAsync(RoleNames.Admin)).Where(u => u.Id != lastAdmin.Id).ToArray())
                Assert.True((await users.RemoveFromRoleAsync(other, RoleNames.Admin)).Succeeded);
            lastAdminId = lastAdmin.Id;
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IUserAccountGateway>()
                .AssignRolesAsync(lastAdminId, [RoleNames.Requester], DateTime.UtcNow, default);
            Assert.False(result.Succeeded);
        }

        await using var verify = factory.Services.CreateAsyncScope();
        var verifyUsers = verify.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True(await verifyUsers.IsInRoleAsync((await verifyUsers.FindByIdAsync(lastAdminId))!, RoleNames.Admin));
    }

    [Fact]
    public async Task UsersInRoleAreLoadedWithNamesAndAllRoles()
    {
        var named = await CreateUserAsync("role-list-named", "Kỹ Thuật Viên A");
        var unnamed = await CreateUserAsync("role-list-unnamed");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            foreach (var email in new[] { named, unnamed })
                Assert.True((await users.AddToRoleAsync((await users.FindByEmailAsync(email))!, RoleNames.Agent)).Succeeded);
        }

        await using var read = factory.Services.CreateAsyncScope();
        var agents = await read.ServiceProvider.GetRequiredService<IUserAccountGateway>().GetUsersInRoleAsync(RoleNames.Agent, default);

        var first = Assert.Single(agents, u => u.Email == named);
        Assert.Equal("Kỹ Thuật Viên A", first.FullName);
        Assert.Equal(new[] { RoleNames.Agent, RoleNames.Requester }, first.Roles.Order(StringComparer.Ordinal));
        var second = Assert.Single(agents, u => u.Email == unnamed);
        Assert.Null(second.FullName);
        Assert.Contains(RoleNames.Agent, second.Roles);
    }

    [Fact]
    public async Task UserDirectoryResolvesRolesAndDisplayNames()
    {
        var email = await CreateUserAsync("directory", "Người Yêu Cầu B");
        var userId = Guid.Parse(await UserIdAsync(email));

        await using var scope = factory.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IUserDirectory>();

        Assert.True(await directory.IsInAnyRoleAsync(userId, [RoleNames.Requester]));
        Assert.False(await directory.IsInAnyRoleAsync(userId, [RoleNames.Agent, RoleNames.Admin]));
        Assert.False(await directory.IsInAnyRoleAsync(Guid.NewGuid(), [RoleNames.Requester]));
        Assert.Equal("Người Yêu Cầu B", await directory.FindDisplayNameAsync(userId));
        Assert.Null(await directory.FindDisplayNameAsync(Guid.NewGuid()));
    }

    private async Task<string> LoginForRefreshTokenAsync(HttpClient client, string email, string password = Password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("refreshToken").GetString()!;
    }

    private async Task<int> ActiveRefreshTokensAsync(string userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().RefreshTokens
            .CountAsync(r => r.UserId == userId && r.RevokedAt == null);
    }

    [Fact]
    public async Task ParallelRefreshWithOneTokenLeavesNoUsableSessionThroughTheEndpoint()
    {
        var email = await CreateUserAsync("refresh-race");
        var userId = await UserIdAsync(email);
        using var client = factory.CreateClient();
        var refreshToken = await LoginForRefreshTokenAsync(client, email);

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken })));

        // Tối đa một request nhận token mới, nhưng các request thua đều thu hồi toàn bộ phiên sau khi token đó
        // đã được lưu, nên không còn refresh token nào dùng được.
        Assert.True(responses.Count(r => r.StatusCode == HttpStatusCode.OK) <= 1);
        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized }));
        Assert.Equal(0, await ActiveRefreshTokensAsync(userId));
    }

    [Fact]
    public async Task ReplayingARotatedTokenRevokesTheReplacementToo()
    {
        var email = await CreateUserAsync("refresh-replay");
        using var client = factory.CreateClient();
        var first = await LoginForRefreshTokenAsync(client, email);

        var rotated = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first });
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        using var body = JsonDocument.Parse(await rotated.Content.ReadAsStringAsync());
        var second = body.RootElement.GetProperty("refreshToken").GetString()!;

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = second })).StatusCode);
    }

    [Fact]
    public async Task ConcurrentRemovalOfTheLastTwoAdministratorsKeepsOne()
    {
        string[] adminIds;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var created = new List<string>();
            foreach (var prefix in new[] { "admin-race-a", "admin-race-b" })
            {
                var admin = (await users.FindByEmailAsync(await CreateUserAsync(prefix)))!;
                Assert.True((await users.AddToRoleAsync(admin, RoleNames.Admin)).Succeeded);
                created.Add(admin.Id);
            }
            foreach (var other in (await users.GetUsersInRoleAsync(RoleNames.Admin)).Where(u => !created.Contains(u.Id)).ToArray())
                Assert.True((await users.RemoveFromRoleAsync(other, RoleNames.Admin)).Succeeded);
            adminIds = created.ToArray();
        }

        async Task<bool> DemoteAsync(string userId)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<IUserAccountGateway>()
                .AssignRolesAsync(userId, [RoleNames.Requester], DateTime.UtcNow, default);
            return result.Succeeded;
        }

        var outcomes = await Task.WhenAll(adminIds.Select(DemoteAsync));

        Assert.Single(outcomes, succeeded => succeeded);
        await using var verify = factory.Services.CreateAsyncScope();
        var remaining = await verify.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().GetUsersInRoleAsync(RoleNames.Admin);
        Assert.Single(remaining);
    }

    [Fact]
    public async Task RoleChangeAndPasswordChangeRevokeSessionsInTheSameTransaction()
    {
        var email = await CreateUserAsync("revoke-on-change");
        var userId = await UserIdAsync(email);
        using var client = factory.CreateClient();
        await LoginForRefreshTokenAsync(client, email);
        Assert.Equal(1, await ActiveRefreshTokensAsync(userId));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IUserAccountGateway>();
            Assert.True((await accounts.AssignRolesAsync(userId, [RoleNames.Agent], DateTime.UtcNow, default)).Succeeded);
        }
        Assert.Equal(0, await ActiveRefreshTokensAsync(userId));

        await LoginForRefreshTokenAsync(client, email);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IUserAccountGateway>();
            Assert.False((await accounts.ChangePasswordAsync(userId, "Wrong@1234", "Changed@1234", DateTime.UtcNow, default)).Succeeded);
        }
        Assert.Equal(1, await ActiveRefreshTokensAsync(userId));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IUserAccountGateway>();
            Assert.True((await accounts.ChangePasswordAsync(userId, Password, "Changed@1234", DateTime.UtcNow, default)).Succeeded);
        }
        Assert.Equal(0, await ActiveRefreshTokensAsync(userId));
    }

    [Fact]
    public async Task ConcurrentOtpRequestsRespectTheHourlyLimitAndLeaveOneValidCode()
    {
        var email = await CreateUserAsync("otp-race");
        for (var i = 0; i < OtpCode.MaxIssuedPerHour - 1; i++)
            await IssueOtpAsync(email, $"10000{i}");

        async Task<bool> RequestAsync(int i)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var now = DateTime.UtcNow;
            return await scope.ServiceProvider.GetRequiredService<IOtpCodeStore>().TryIssueAsync(
                OtpCode.Issue(email, OtpCode.ResetPasswordPurpose, Hash($"20000{i}"), now), OtpCode.MaxIssuedPerHour, now.AddHours(-1));
        }

        var issued = await Task.WhenAll(Enumerable.Range(0, 6).Select(RequestAsync));

        Assert.Single(issued, won => won);
        await using var check = factory.Services.CreateAsyncScope();
        var codes = check.ServiceProvider.GetRequiredService<ApplicationDbContext>().OtpCodes.Where(o => o.Target == email);
        Assert.Equal(OtpCode.MaxIssuedPerHour, await codes.CountAsync());
        Assert.Equal(1, await codes.CountAsync(o => !o.IsUsed));
    }

    [Fact]
    public async Task ForgotPasswordAnswersAtOnceAndIssuesTheCodeInTheBackground()
    {
        var email = await CreateUserAsync("forgot-queue");
        using var client = factory.CreateClient();

        var known = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        var unknown = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = $"nobody-{Guid.NewGuid():N}@test.invalid" });

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        var deadline = DateTime.UtcNow.AddSeconds(15);
        var issued = false;
        while (!issued && DateTime.UtcNow < deadline)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            issued = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OtpCodes.AnyAsync(o => o.Target == email);
            if (!issued) await Task.Delay(100);
        }
        Assert.True(issued);
    }

    /// <summary>
    /// Mô phỏng một giao dịch đang tạo refresh token (đăng nhập hoặc xoay vòng) giữ khóa người dùng; thao tác thu hồi
    /// chạy song song phải chờ nó commit rồi thu hồi luôn token mới.
    /// </summary>
    private Task RevocationWaitsForAnInFlightSessionAsync(string userId, Func<IServiceProvider, Task> revoke) =>
        RevocationWaitsForAnInFlightSessionAsync(userId, RefreshToken.Issue(userId, Hash(Guid.NewGuid().ToString()), DateTime.UtcNow), revoke);

    private async Task RevocationWaitsForAnInFlightSessionAsync(string userId, RefreshToken inFlight, Func<IServiceProvider, Task> revoke)
    {
        await using var holder = factory.Services.CreateAsyncScope();
        var context = holder.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM public.\"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE");
        context.RefreshTokens.Add(inFlight);
        await context.SaveChangesAsync();

        async Task RunAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await revoke(scope.ServiceProvider);
        }

        var revoking = RunAsync();
        await Task.Delay(500);
        Assert.False(revoking.IsCompleted, "Thao tác thu hồi phải chờ giao dịch đang giữ khóa người dùng.");
        await transaction.CommitAsync();
        await revoking;

        Assert.Equal(0, await ActiveRefreshTokensAsync(userId));
    }

    [Fact]
    public async Task PasswordChangeRevokesASessionCreatedConcurrently()
    {
        var email = await CreateUserAsync("lock-password");
        var userId = await UserIdAsync(email);
        await RevocationWaitsForAnInFlightSessionAsync(userId, async services => Assert.True((await services
            .GetRequiredService<IUserAccountGateway>().ChangePasswordAsync(userId, Password, "Changed@1234", DateTime.UtcNow, default)).Succeeded));
    }

    [Fact]
    public async Task RoleChangeRevokesASessionCreatedConcurrently()
    {
        var email = await CreateUserAsync("lock-role");
        var userId = await UserIdAsync(email);
        await RevocationWaitsForAnInFlightSessionAsync(userId, async services => Assert.True((await services
            .GetRequiredService<IUserAccountGateway>().AssignRolesAsync(userId, [RoleNames.Agent], DateTime.UtcNow, default)).Succeeded));
    }

    [Fact]
    public async Task ReuseDetectionRevokesAReplacementCreatedConcurrently()
    {
        var userId = await UserIdAsync(await CreateUserAsync("lock-reuse"));
        var current = await SeedTokenAsync(userId);
        await RevocationWaitsForAnInFlightSessionAsync(userId, current.Rotate(Hash(Guid.NewGuid().ToString()), DateTime.UtcNow), services =>
            services.GetRequiredService<IRefreshTokenStore>().RevokeFamilyAsync(userId, current.FamilyId, DateTime.UtcNow));
    }

    [Fact]
    public async Task LogoutWithTheOldTokenRevokesAReplacementCreatedConcurrently()
    {
        var userId = await UserIdAsync(await CreateUserAsync("lock-logout"));
        var raw = Guid.NewGuid().ToString();
        var current = RefreshToken.Issue(userId, Hash(raw), DateTime.UtcNow);
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            var context = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.RefreshTokens.Add(current);
            await context.SaveChangesAsync();
        }

        await RevocationWaitsForAnInFlightSessionAsync(userId, current.Rotate(Hash(Guid.NewGuid().ToString()), DateTime.UtcNow), services =>
            services.GetRequiredService<IRefreshTokenStore>().RevokeSessionByTokenAsync(Hash(raw), DateTime.UtcNow));
    }

    [Fact]
    public async Task LogoutAfterRotationWithTheStaleTokenEndsTheSessionButKeepsOtherDevices()
    {
        var email = await CreateUserAsync("logout-stale");
        var userId = await UserIdAsync(email);
        using var client = factory.CreateClient();
        var otherDevice = await LoginForRefreshTokenAsync(client, email);
        var first = await LoginForRefreshTokenAsync(client, email);
        var rotated = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first });
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        using var body = JsonDocument.Parse(await rotated.Content.ReadAsStringAsync());
        var second = body.RootElement.GetProperty("refreshToken").GetString()!;

        // Client đăng xuất bằng token cũ (chưa kịp lưu token mới): token mới cũng phải mất hiệu lực.
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/revoke", new { refreshToken = first })).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = second })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = otherDevice })).StatusCode);
        Assert.Equal(1, await ActiveRefreshTokensAsync(userId));
    }

    [Fact]
    public async Task PasswordResetRevokesASessionCreatedConcurrently()
    {
        var email = await CreateUserAsync("lock-reset");
        var userId = await UserIdAsync(email);
        await IssueOtpAsync(email, "778899");
        await RevocationWaitsForAnInFlightSessionAsync(userId, async services => Assert.True((await services
            .GetRequiredService<IPasswordResetGateway>().ResetWithOtpAsync(email, Hash("778899"), "Changed@1234", DateTime.UtcNow)).Succeeded));
    }
}
