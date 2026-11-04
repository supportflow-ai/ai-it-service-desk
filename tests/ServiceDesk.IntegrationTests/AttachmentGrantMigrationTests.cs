using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ServiceDesk.Domain.Ticketing;
using ServiceDesk.Infrastructure.Persistence;
using ServiceDesk.Infrastructure.Ticketing;
using Testcontainers.PostgreSql;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class AttachmentGrantMigrationTests
{
    [Fact]
    public async Task UpgradePreservesLegacyFilesAndLinksOnlyUnambiguousConsumedGrants()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("attachment_grant_upgrade_tests").WithUsername("test").WithPassword("test").Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await postgres.StartAsync(timeout.Token);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var context = new ApplicationDbContext(options);
        await context.GetService<IMigrator>().MigrateAsync("20261103090000_AddAttachmentUploadGrant", timeout.Token);
        var now = DateTime.UtcNow;
        var actor = Guid.NewGuid();
        var ticket = Ticket.Create(actor, "Legacy attachment", "Migration compatibility verification", TicketCategories.VPN, now);
        await new PostgresTicketWriter(context).CreateAsync(ticket, null, timeout.Token);

        async Task<(Guid Grant, Guid? Attachment, string Key)> Seed(string label, int matches, bool consumed, bool otherActor = false)
        {
            var id = Guid.NewGuid();
            var key = $"tickets/{ticket.Id}/{Guid.NewGuid()}_{label}.txt";
            Guid? attachmentId = null;
            for (var i = 0; i < matches; i++)
            {
                var attachment = TicketAttachment.Create(ticket.Id, label + ".txt", 10, "text/plain", key,
                    otherActor ? Guid.NewGuid() : actor, now);
                context.TicketAttachments.Add(attachment);
                attachmentId = attachment.Id;
            }
            await context.SaveChangesAsync(timeout.Token);
            DateTime? consumedAt = consumed ? now : null;
            // Chỉ ghi cột của schema cũ; model hiện tại đã có attachment_id nên không dùng DbSet.Add(grant).
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO public.attachment_upload_grants
                    ("Id", ticket_id, actor_id, file_name, file_size_bytes, content_type, storage_key, expires_at, consumed_at)
                VALUES ({id}, {ticket.Id}, {actor}, {label + ".txt"}, {10L}, {"text/plain"}, {key}, {now.AddHours(1)}, {consumedAt})
                """, timeout.Token);
            return (id, attachmentId, key);
        }

        var unique = await Seed("unique", 1, true);
        var ambiguous = await Seed("ambiguous", 2, true);
        var missing = await Seed("missing", 0, true);
        var foreign = await Seed("foreign", 1, true, otherActor: true);
        var pending = await Seed("pending", 0, false);
        var before = await context.TicketAttachments.AsNoTracking().OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.StorageKey, a.UploadedBy, a.FileSizeBytes }).ToListAsync(timeout.Token);

        await context.Database.MigrateAsync(timeout.Token);
        await context.Database.MigrateAsync(timeout.Token); // Chạy lại không thay dữ liệu hoặc nối trùng.
        var grants = await context.AttachmentUploadGrants.AsNoTracking().ToDictionaryAsync(g => g.Id, timeout.Token);
        Assert.Equal(unique.Attachment, grants[unique.Grant].AttachmentId);
        foreach (var item in new[] { ambiguous, missing, foreign, pending }) Assert.Null(grants[item.Grant].AttachmentId);
        Assert.NotNull(grants[missing.Grant].ConsumedAt);
        Assert.Null(grants[pending.Grant].ConsumedAt);
        var after = await context.TicketAttachments.AsNoTracking().OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.StorageKey, a.UploadedBy, a.FileSizeBytes }).ToListAsync(timeout.Token);
        Assert.Equal(before, after);
        var linked = await new PostgresTicketAttachmentStore(context).GetByIdAsync(unique.Attachment!.Value, timeout.Token);
        Assert.Equal(unique.Key, linked!.StorageKey);
        Assert.False(context.Database.HasPendingModelChanges());
        var applied = (await context.Database.GetAppliedMigrationsAsync(timeout.Token)).ToArray();
        Assert.Contains("20261103090001_LinkAttachmentUploadGrant", applied);
        Assert.DoesNotContain(applied, name => name.Contains("AddAttachmentUploadIntents", StringComparison.Ordinal));
    }
}
