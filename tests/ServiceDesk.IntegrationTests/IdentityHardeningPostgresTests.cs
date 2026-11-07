using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Identity.Domain;
using ServiceDesk.Infrastructure.Persistence;
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
            .IssueAsync(OtpCode.Issue(email, OtpCode.ResetPasswordPurpose, Hash(code), DateTime.UtcNow));
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
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IUserAccountGateway>().CheckPasswordAsync(email, password, default) is not null;
    }

    [Fact]
    public async Task ConcurrentRotationOfOneRefreshTokenRevokesItExactlyOnce()
    {
        var token = RefreshToken.Issue(Guid.NewGuid().ToString(), Hash(Guid.NewGuid().ToString()), DateTime.UtcNow);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IRefreshTokenStore>().SaveTokenAsync(token);

        async Task<bool> RotateAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRefreshTokenStore>().TryRevokeAsync(token.Id, DateTime.UtcNow);
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => RotateAsync()));

        Assert.Single(results, won => won);
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
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRefreshTokenStore>()
                .SaveTokenAsync(RefreshToken.Issue(userId, Hash(Guid.NewGuid().ToString()), DateTime.UtcNow));
        }
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
                .AssignRolesAsync(lastAdminId, [RoleNames.Requester], default);
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
}
