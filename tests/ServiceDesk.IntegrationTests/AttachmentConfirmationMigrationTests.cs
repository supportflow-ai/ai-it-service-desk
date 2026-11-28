using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.Modules.Ticketing.Infrastructure;
using ServiceDesk.Platform.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class AttachmentConfirmationMigrationTests
{
    [Fact]
    public async Task UpgradePreservesPendingAndConfirmedGrantsAndDoesNotMarkOldFilesClean()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("attachment_lease_upgrade").WithUsername("test").WithPassword("test").Build();
        await postgres.StartAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
        await db.GetService<IMigrator>().MigrateAsync("20261128112618_AddDurablePasswordResetJobs");
        var actor = Guid.NewGuid(); var now = DateTime.UtcNow;
        var ticket = Ticket.Create(actor, "Lease upgrade", "Old data must remain valid", TicketCategories.NET, now);
        var file = TicketAttachment.Create(ticket.Id, "file.txt", 5, "text/plain", $"tickets/{ticket.Id}/legacy.txt", actor, now);
        db.Tickets.Add(ticket); db.TicketAttachments.Add(file); await db.SaveChangesAsync();
        var pending = AttachmentUploadGrant.CreatePending(ticket.Id, actor, "file.txt", 5, "text/plain", now.AddMinutes(15));
        var confirmed = AttachmentUploadGrant.CreatePending(ticket.Id, actor, "file.txt", 5, "text/plain", now.AddMinutes(15));
        foreach (var grant in new[] { pending, confirmed })
        {
            DateTime? consumed = grant.Id == confirmed.Id ? now : null; Guid? attachment = grant.Id == confirmed.Id ? file.Id : null;
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO public.attachment_upload_grants (\"Id\", ticket_id, actor_id, file_name, file_size_bytes, content_type, storage_key, expires_at, consumed_at, attachment_id) VALUES ({grant.Id}, {grant.TicketId}, {grant.ActorId}, {grant.FileName}, {grant.FileSizeBytes}, {grant.ContentType}, {grant.StorageKey}, {grant.ExpiresAt}, {consumed}, {attachment})");
        }
        await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        var grants = await db.AttachmentUploadGrants.OrderBy(value => value.Id).ToListAsync();
        Assert.Equal(2, grants.Count); Assert.All(grants, grant => { Assert.Null(grant.ConfirmationAttemptId); Assert.Null(grant.ConfirmationLeaseUntil); });
        Assert.Equal(file.Id, grants.Single(value => value.Id == confirmed.Id).AttachmentId);
        Assert.Equal(file.StorageKey, (await db.TicketAttachments.SingleAsync()).StorageKey);
        var store = new PostgresTicketAttachmentStore(db);
        var reserved = await store.ReserveConfirmationAsync(pending.Id, Guid.NewGuid());
        Assert.StartsWith($"tickets/{ticket.Id}/attachments/v2/{pending.Id:N}/", reserved.ConfirmationStorageKey());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync()); Assert.False(db.Database.HasPendingModelChanges());
    }
}
