using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ServiceDesk.Modules.Identity.Domain;
using ServiceDesk.Platform.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class PasswordResetJobMigrationTests
{
    [Fact]
    public async Task UpgradeAddsOnlyTheQueueAndPreservesIdentityOtpAndRefreshData()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("otp_upgrade_tests").WithUsername("test").WithPassword("test").Build();
        await postgres.StartAsync();
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options);
        var migrations = context.Database.GetMigrations().ToArray();
        Assert.EndsWith("_AddDurablePasswordResetJobs", migrations[^1]);
        await context.GetService<IMigrator>().MigrateAsync(migrations[^2]);
        var user = new IdentityUser { Id = Guid.NewGuid().ToString(), UserName = "upgrade@test.invalid", SecurityStamp = "original-stamp" };
        var otp = OtpCode.Issue(user.UserName, OtpCode.ResetPasswordPurpose, "legacy-digest", DateTime.UtcNow);
        var refresh = RefreshToken.Issue(user.Id, "refresh-digest", DateTime.UtcNow);
        context.Users.Add(user);
        context.OtpCodes.Add(otp);
        context.RefreshTokens.Add(refresh);
        await context.SaveChangesAsync();

        await context.Database.MigrateAsync();

        context.ChangeTracker.Clear();
        Assert.Equal("original-stamp", (await context.Users.SingleAsync()).SecurityStamp);
        Assert.Equal("legacy-digest", (await context.OtpCodes.SingleAsync()).CodeHash);
        var savedRefresh = await context.RefreshTokens.SingleAsync();
        Assert.Equal(refresh.FamilyId, savedRefresh.FamilyId);
        Assert.Equal("refresh-digest", savedRefresh.TokenHash);
        Assert.Null(savedRefresh.RevokedAt);
        Assert.Empty(await context.PasswordResetJobs.ToListAsync());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
