using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ServiceDesk.Platform.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace ServiceDesk.IntegrationTests;

/// <summary>
/// Nâng cấp cơ sở dữ liệu có refresh token cũ lên chuỗi phiên (<c>family_id</c>), và bản API cũ (không biết cột này)
/// vẫn ghi được refresh token sau migration trong lúc triển khai cuốn chiếu.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RefreshTokenFamilyMigrationTests
{
    private const string BeforeFamily = "20261107090000_AddOtpFailedAttempts";

    [Fact]
    public async Task UpgradeBackfillsFamiliesAndLegacyWritersKeepWorking()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("refresh_family_upgrade_tests").WithUsername("test").WithPassword("test").Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await postgres.StartAsync(timeout.Token);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var context = new ApplicationDbContext(options);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeFamily, timeout.Token);

        // Câu INSERT của bản API cũ: chỉ các cột của schema trước khi có family_id.
        async Task<Guid> InsertLegacyAsync()
        {
            var id = Guid.NewGuid();
            var now = DateTime.UtcNow;
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO public.refresh_tokens (id, user_id, token_hash, expires_at, created_at)
                VALUES ({id}, {"legacy-user"}, {Guid.NewGuid().ToString("N")}, {now.AddDays(7)}, {now})
                """, timeout.Token);
            return id;
        }

        var existing = await InsertLegacyAsync();

        await context.Database.MigrateAsync(timeout.Token);
        await context.Database.MigrateAsync(timeout.Token); // Chạy lại không đổi dữ liệu.

        var writtenAfterUpgrade = await InsertLegacyAsync();
        var families = await context.RefreshTokens.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.FamilyId, timeout.Token);
        Assert.Equal(existing, families[existing]);
        Assert.Equal(writtenAfterUpgrade, families[writtenAfterUpgrade]);
        Assert.False(context.Database.HasPendingModelChanges());

        // Quay lui về trước chuỗi phiên rồi nâng cấp lại: trigger và cột được gỡ, rồi tạo lại sạch.
        await migrator.MigrateAsync(BeforeFamily, timeout.Token);
        var afterRollback = await InsertLegacyAsync();
        await context.Database.MigrateAsync(timeout.Token);
        Assert.Equal(afterRollback, await context.RefreshTokens.AsNoTracking()
            .Where(t => t.Id == afterRollback).Select(t => t.FamilyId).SingleAsync(timeout.Token));
    }
}
