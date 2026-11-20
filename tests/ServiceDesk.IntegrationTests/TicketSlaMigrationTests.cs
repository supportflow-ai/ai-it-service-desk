using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.Platform.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace ServiceDesk.IntegrationTests;

/// <summary>
/// Kiểm chứng migration AddTicketSlaSupport trên PostgreSQL Testcontainers:
/// - Migrate tới migration liền trước.
/// - Ghi dữ liệu legacy bằng schema cũ (không có cột SLA).
/// - Áp dụng migration AddTicketSlaSupport.
/// - Đối chiếu: dữ liệu nghiệp vụ cũ được giữ nguyên; legacy IsSlaApplicable = false; không bịa target/marker.
/// - Round-trip các trường SLA mới: duration dưới 1 phút (ví dụ 45 giây) và timestamp precision.
/// - Rollback về migration trước và nâng cấp lại.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TicketSlaMigrationTests
{
    private const string BeforeSla = "20261108130000_BackfillLegacyTicketsToManualTriage";

    [Fact]
    public async Task UpgradePreservesLegacyTicketsAndSupportsNewSlaFields()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("ticket_sla_migration_tests")
            .WithUsername("test")
            .WithPassword("test")
            .Build();

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await postgres.StartAsync(timeout.Token);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        var migrator = context.GetService<IMigrator>();

        // 1. Migrate tới mốc ngay trước AddTicketSlaSupport
        await migrator.MigrateAsync(BeforeSla, timeout.Token);

        // 2. Chèn dữ liệu legacy đại diện (chỉ dùng các cột của schema cũ)
        var legacyId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var legacyCreatedAt = new DateTime(2026, 11, 1, 8, 30, 0, DateTimeKind.Utc);

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.tickets (
                "Id", requester_id, title, description, category_id, status, created_at, updated_at, support_team_id, impact, urgency, priority
            ) VALUES (
                {legacyId}, {requesterId}, {"Legacy Ticket"}, {"Ticket created before SLA was introduced"}, {"SW"}, {1}, {legacyCreatedAt}, {legacyCreatedAt}, {SupportTeam.ManualTriageId}, {1}, {1}, {2}
            )
            """, timeout.Token);

        // 3. Áp dụng migration mới AddTicketSlaSupport
        await context.Database.MigrateAsync(timeout.Token);
        await context.Database.MigrateAsync(timeout.Token); // Re-run idempotent, không đổi dữ liệu

        // 4. Assert: ticket legacy được bảo toàn nguyên vẹn và mang cờ NotApplicable (IsSlaApplicable = false)
        var legacyTicket = await context.Set<Ticket>().AsNoTracking()
            .SingleAsync(t => t.Id == legacyId, timeout.Token);

        legacyTicket.Title.Should().Be("Legacy Ticket");
        legacyTicket.Description.Should().Be("Ticket created before SLA was introduced");
        legacyTicket.IsSlaApplicable.Should().BeFalse();
        legacyTicket.FirstResponseTargetMinutes.Should().BeNull();
        legacyTicket.FirstRespondedAt.Should().BeNull();
        legacyTicket.FirstResponseBreached.Should().BeFalse();
        legacyTicket.ResolutionTargetMinutes.Should().BeNull();
        legacyTicket.ResolutionAccumulatedDuration.Should().Be(TimeSpan.Zero);
        legacyTicket.ResolutionSegmentStartedAt.Should().BeNull();
        legacyTicket.ResolvedAt.Should().BeNull();
        legacyTicket.ResolutionBreached.Should().BeFalse();

        // 5. Chèn ticket mới có áp dụng SLA và round-trip
        var newTicketCreatedAt = new DateTime(2026, 11, 17, 10, 15, 30, 123, DateTimeKind.Utc);
        var newTicket = Ticket.Create(
            requesterId: requesterId,
            title: "New Ticket with SLA",
            description: "Testing SLA persistence with sub-minute duration",
            categoryId: "HW",
            createdAt: newTicketCreatedAt);

        var p1Targets = PrioritySlaTargets.GetFor(Priority.P1);
        newTicket.InitializeSla(p1Targets, newTicketCreatedAt);

        // Mô phỏng chạy 45 giây rồi tạm dừng (duration dưới 1 phút)
        var pauseAt = newTicketCreatedAt.AddSeconds(45);
        newTicket.PauseResolution(pauseAt);

        context.Set<Ticket>().Add(newTicket);
        await context.SaveChangesAsync(timeout.Token);

        // Đọc lại từ database
        var reloaded = await context.Set<Ticket>().AsNoTracking()
            .SingleAsync(t => t.Id == newTicket.Id, timeout.Token);

        reloaded.IsSlaApplicable.Should().BeTrue();
        reloaded.FirstResponseTargetMinutes.Should().Be(60);
        reloaded.ResolutionTargetMinutes.Should().Be(240);
        reloaded.ResolutionAccumulatedDuration.Should().Be(TimeSpan.FromSeconds(45));
        reloaded.ResolutionSegmentStartedAt.Should().BeNull();
        reloaded.FirstResponseBreached.Should().BeFalse();
        reloaded.ResolutionBreached.Should().BeFalse();

        // 6. Rollback về migration trước đó: các cột SLA được gỡ bỏ sạch sẽ
        await migrator.MigrateAsync(BeforeSla, timeout.Token);

        // Dữ liệu legacy vẫn tồn tại sau khi rollback migration
        var legacyAfterRollbackTitle = await context.Database
            .SqlQuery<string>($"SELECT title AS \"Value\" FROM public.tickets WHERE \"Id\" = {legacyId}")
            .SingleAsync(timeout.Token);
        legacyAfterRollbackTitle.Should().Be("Legacy Ticket");

        // 7. Nâng cấp lại lên bản mới nhất
        await context.Database.MigrateAsync(timeout.Token);
        context.Database.HasPendingModelChanges().Should().BeFalse();
    }
}
