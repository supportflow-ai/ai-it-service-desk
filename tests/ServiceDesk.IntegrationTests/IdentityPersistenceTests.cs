using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Application.Identity.Ports;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Infrastructure.Persistence;
using Xunit;
namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class IdentityPersistenceTests(ServiceDeskWebApplicationFactory factory) : IClassFixture<ServiceDeskWebApplicationFactory>
{
    [Fact]
    public async Task LockoutIsPersistedAcrossScopes_ExpiresAndSuccessfulLoginResetsFailures()
    {
        var email = $"lockout-{Guid.NewGuid()}@test.invalid";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var gateway = scope.ServiceProvider.GetRequiredService<IUserAccountGateway>();
            Assert.NotNull((await gateway.CreateRequesterAsync(email, "Test@1234", null, default)).User);
        }
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            Assert.Null(await scope.ServiceProvider.GetRequiredService<IUserAccountGateway>().CheckPasswordAsync(email, "wrong", default));
        }
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = (await users.FindByEmailAsync(email))!;
            Assert.True(await users.IsLockedOutAsync(user));
            Assert.Null(await scope.ServiceProvider.GetRequiredService<IUserAccountGateway>().CheckPasswordAsync(email, "Test@1234", default));
            await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(-1));
        }
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var gateway = scope.ServiceProvider.GetRequiredService<IUserAccountGateway>();
            Assert.NotNull(await gateway.CheckPasswordAsync(email, "Test@1234", default));
            Assert.Null(await gateway.CheckPasswordAsync(email, "wrong", default));
            Assert.NotNull(await gateway.CheckPasswordAsync(email, "Test@1234", default));
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Assert.Equal(0, (await users.FindByEmailAsync(email))!.AccessFailedCount);
        }
    }

    [Fact]
    public async Task FullNameClaimPersistsAndIsReturnedByFreshGateway()
    {
        var email = $"fullname-{Guid.NewGuid()}@test.invalid";
        string userId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IUserAccountGateway>()
                .CreateRequesterAsync(email, "Test@1234", "  Example User  ", default);
            userId = result.User!.UserId;
            Assert.Equal("Example User", result.User.FullName);
        }

        await using var freshScope = factory.Services.CreateAsyncScope();
        var user = await freshScope.ServiceProvider.GetRequiredService<IUserAccountGateway>()
            .FindByIdAsync(userId, default);
        Assert.Equal("Example User", user!.FullName);
    }

    [Fact]
    public async Task RoleWriteFailureRollsBackUserCreation()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var email = $"rollback-{Guid.NewGuid()}@test.invalid";
        await context.Database.ExecuteSqlRawAsync("""
            CREATE OR REPLACE FUNCTION fail_test_role_write() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'synthetic role write failure'; END $$;
            CREATE TRIGGER fail_role_write BEFORE INSERT ON "AspNetUserRoles"
                FOR EACH ROW EXECUTE FUNCTION fail_test_role_write();
            """);
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => scope.ServiceProvider.GetRequiredService<IUserAccountGateway>()
                .CreateRequesterAsync(email, "Test@1234", null, default));
            context.ChangeTracker.Clear();
            Assert.False(await context.Users.AnyAsync(u => u.Email == email));
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_role_write ON \"AspNetUserRoles\"; DROP FUNCTION fail_test_role_write();");
        }
    }
}
