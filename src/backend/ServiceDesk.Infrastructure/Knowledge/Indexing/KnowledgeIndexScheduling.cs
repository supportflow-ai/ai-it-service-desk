using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Knowledge.Indexing;
using ServiceDesk.Domain.Knowledge;
using ServiceDesk.Infrastructure.Persistence;

namespace ServiceDesk.Infrastructure.Knowledge.Indexing;

internal static class KnowledgeIndexScheduling
{
    // Caller owns a transaction and has locked/updated the article first.
    internal static async Task ScheduleAsync(ApplicationDbContext db, Article article, ArticleVersion version,
        Guid jobId, DateTime now, bool invalidateActive, CancellationToken ct)
    {
        await db.KnowledgeIndexJobs.Where(x => x.ArticleId == article.Id &&
            (x.Status == "Pending" || x.Status == "Running"))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Superseded")
                .SetProperty(x => x.CompletedAt, now), ct);
        db.KnowledgeIndexJobs.Add(new KnowledgeIndexJob
        {
            Id = jobId, ArticleId = article.Id, ArticleVersionId = version.Id, VersionNumber = version.VersionNumber,
            SourceHash = KnowledgeIndexProfile.SnapshotHash(version.Title, version.Content),
            ProfileId = KnowledgeIndexProfile.Id, ProfileHash = KnowledgeIndexProfile.Fingerprint,
            CreatedAt = now, NextAttemptAt = now
        });
        var state = await db.KnowledgeIndexStates.SingleOrDefaultAsync(x => x.ArticleId == article.Id, ct);
        if (state is null)
        {
            state = new KnowledgeIndexState { ArticleId = article.Id };
            db.KnowledgeIndexStates.Add(state);
        }
        state.RequestedJobId = jobId;
        if (invalidateActive) state.ActiveIndexSetId = null;
    }

    internal static async Task ArchiveAsync(ApplicationDbContext db, Guid articleId, DateTime now, CancellationToken ct)
    {
        await db.KnowledgeIndexStates.Where(x => x.ArticleId == articleId).ExecuteUpdateAsync(s =>
            s.SetProperty(x => x.RequestedJobId, (Guid?)null).SetProperty(x => x.ActiveIndexSetId, (Guid?)null), ct);
        await db.KnowledgeIndexJobs.Where(x => x.ArticleId == articleId &&
            (x.Status == "Pending" || x.Status == "Running"))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Superseded")
                .SetProperty(x => x.CompletedAt, now), ct);
    }
}
