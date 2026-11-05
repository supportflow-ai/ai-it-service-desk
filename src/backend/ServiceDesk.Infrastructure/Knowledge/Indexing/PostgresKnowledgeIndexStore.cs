using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Common;
using ServiceDesk.Application.Knowledge.Indexing;
using ServiceDesk.Domain.Knowledge;
using ServiceDesk.Infrastructure.Persistence;

namespace ServiceDesk.Infrastructure.Knowledge.Indexing;

internal sealed class PostgresKnowledgeIndexStore(ApplicationDbContext db) : IKnowledgeIndexStore
{
    public async Task<KnowledgeIndexLease?> ClaimAsync(DateTime now, TimeSpan leaseDuration, int maxAttempts, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE public.knowledge_index_jobs SET "Status" = 'Failed', "ErrorCode" = 'lease_attempts_exhausted',
                "CompletedAt" = {now}
            WHERE "Status" = 'Running' AND "LeaseUntil" <= {now} AND "Attempts" >= {maxAttempts}
            """, ct);
        var token = Guid.NewGuid();
        var until = now + leaseDuration;
        var jobs = await db.KnowledgeIndexJobs.FromSqlInterpolated($"""
            UPDATE public.knowledge_index_jobs AS job SET "Status" = 'Running', "Attempts" = job."Attempts" + 1,
                "LeaseToken" = {token}, "LeaseUntil" = {until}, "ErrorCode" = NULL
            WHERE job."Id" = (
                SELECT "Id" FROM public.knowledge_index_jobs
                WHERE (("Status" = 'Pending' AND "NextAttemptAt" <= {now})
                    OR ("Status" = 'Running' AND "LeaseUntil" <= {now})) AND "Attempts" < {maxAttempts}
                ORDER BY "CreatedAt", "Id" FOR UPDATE SKIP LOCKED LIMIT 1
            ) RETURNING job.*
            """).AsNoTracking().ToListAsync(ct);
        var job = jobs.SingleOrDefault();
        if (job is null) return null;
        var version = await db.ArticleVersions.AsNoTracking().SingleAsync(x => x.Id == job.ArticleVersionId, ct);
        var snapshot = new KnowledgeIndexSnapshot(job.Id, job.ArticleId, version.Id, version.VersionNumber,
            version.Title, version.Content);
        var lease = new KnowledgeIndexLease(job.Id, token, snapshot);
        if (job.ProfileId != KnowledgeIndexProfile.Id || job.ProfileHash != KnowledgeIndexProfile.Fingerprint ||
            job.SourceHash != snapshot.SourceHash || version.ArticleId != job.ArticleId ||
            version.VersionNumber != job.VersionNumber)
        {
            await FailAsync(lease, "snapshot_or_profile_changed", false, now, maxAttempts, ct);
            return null;
        }
        return lease;
    }

    public async Task<bool> CompleteAsync(KnowledgeIndexLease lease, PreparedKnowledgeIndex result, DateTime now, CancellationToken ct)
    {
        result.Validate(lease.Snapshot);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Lock order: article -> job/state. Publish, archive and rebuild use the same article lock.
        var article = await db.Articles.FromSqlInterpolated($"""
            SELECT *, xmin FROM public.articles WHERE "Id" = {lease.Snapshot.ArticleId} FOR UPDATE
            """).AsNoTracking().SingleAsync(ct);
        var job = (await db.KnowledgeIndexJobs.FromSqlInterpolated($"""
            SELECT * FROM public.knowledge_index_jobs WHERE "Id" = {lease.JobId}
                AND "Status" = 'Running' AND "LeaseToken" = {lease.LeaseToken} AND "LeaseUntil" > {now}
            FOR UPDATE
            """).ToListAsync(ct)).SingleOrDefault();
        if (job is null) return false;
        var state = await db.KnowledgeIndexStates.SingleOrDefaultAsync(x => x.ArticleId == article.Id, ct);
        if (article.Status != ArticleStatus.Published || article.Version != lease.Snapshot.VersionNumber ||
            state?.RequestedJobId != job.Id)
        {
            job.Status = "Superseded";
            job.CompletedAt = now;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return false;
        }

        db.KnowledgeIndexSets.Add(new KnowledgeIndexSet
        {
            Id = job.Id, ArticleId = article.Id, ArticleVersionId = result.ArticleVersionId,
            VersionNumber = result.VersionNumber, SourceHash = result.SourceHash,
            NormalizedContentHash = result.NormalizedContentHash, ProfileId = result.ProfileId,
            ProfileHash = result.ProfileHash, ProfileJson = KnowledgeIndexProfile.Json,
            Dimension = KnowledgeIndexProfile.Dimension, ChunkCount = result.Chunks.Count, CreatedAt = now
        });
        foreach (var chunk in result.Chunks)
            db.KnowledgeChunks.Add(new KnowledgeChunk
            {
                IndexSetId = job.Id, Index = chunk.Index, ChunkId = chunk.ChunkId, Text = chunk.Text,
                TextHash = chunk.TextHash, StartChar = chunk.StartChar, EndChar = chunk.EndChar,
                StartLine = chunk.StartLine, EndLine = chunk.EndLine, HeadingPath = chunk.HeadingPath,
                TokenCount = chunk.TokenCount
            });
        await db.SaveChangesAsync(ct);
        // Keep vector-specific SQL in Infrastructure; Application/Domain have no pgvector/EF dependency.
        foreach (var chunk in result.Chunks)
        {
            var vector = "[" + string.Join(",", chunk.Embedding.Select(v => v.ToString("R", CultureInfo.InvariantCulture))) + "]";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO public.knowledge_chunk_vectors ("IndexSetId", "ChunkIndex", "Embedding")
                VALUES ({job.Id}, {chunk.Index}, CAST({vector} AS vector(384)))
                ON CONFLICT ("IndexSetId", "ChunkIndex") DO UPDATE SET "Embedding" = EXCLUDED."Embedding"
                """, ct);
        }
        state!.ActiveIndexSetId = job.Id;
        job.Status = "Succeeded";
        job.ErrorCode = null;
        job.CompletedAt = now;
        job.LeaseToken = null;
        job.LeaseUntil = null;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    public async Task FailAsync(KnowledgeIndexLease lease, string errorCode, bool retryable, DateTime now, int maxAttempts, CancellationToken ct)
    {
        var job = await db.KnowledgeIndexJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == lease.JobId &&
            x.Status == "Running" && x.LeaseToken == lease.LeaseToken && x.LeaseUntil > now, ct);
        if (job is null) return;
        var terminal = !retryable || job.Attempts >= maxAttempts;
        var status = terminal ? "Failed" : "Pending";
        var next = now.AddSeconds(Math.Min(60, Math.Pow(2, job.Attempts)));
        await db.KnowledgeIndexJobs.Where(x => x.Id == lease.JobId && x.Status == "Running" &&
            x.LeaseToken == lease.LeaseToken && x.LeaseUntil > now).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, status).SetProperty(x => x.ErrorCode, errorCode)
                .SetProperty(x => x.NextAttemptAt, next).SetProperty(x => x.LeaseToken, (Guid?)null)
                .SetProperty(x => x.LeaseUntil, (DateTime?)null)
                .SetProperty(x => x.CompletedAt, terminal ? now : (DateTime?)null), ct);
    }

    public async Task<Guid> RequestRebuildAsync(Guid articleId, Guid requestId, uint expectedRevision, DateTime now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var locked = await db.Articles.FromSqlInterpolated($"""
            SELECT *, xmin FROM public.articles WHERE "Id" = {articleId} FOR UPDATE
            """).AsNoTracking().Select(a => new { Article = a, Revision = EF.Property<uint>(a, "xmin") }).SingleAsync(ct);
        var article = locked.Article;
        if (locked.Revision != expectedRevision) throw new ConcurrencyConflictException("Phiên bản bài viết đã thay đổi.");
        if (article.Status != ArticleStatus.Published) throw new InvalidOperationException("Bài không còn được công bố.");
        var version = await db.ArticleVersions.AsNoTracking().SingleAsync(x => x.ArticleId == articleId &&
            x.VersionNumber == article.Version, ct);
        var existing = await db.KnowledgeIndexJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (existing is not null)
        {
            if (existing.ArticleId != articleId || existing.ArticleVersionId != version.Id ||
                existing.ProfileHash != KnowledgeIndexProfile.Fingerprint)
                throw new ConcurrencyConflictException("RequestId đã được dùng cho một snapshot khác.");
            await transaction.CommitAsync(ct);
            return existing.Id;
        }
        await KnowledgeIndexScheduling.ScheduleAsync(db, article, version, requestId, now, invalidateActive: false, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return requestId;
    }

    public async Task<KnowledgeIndexStatusDto> GetStatusAsync(Guid articleId, CancellationToken ct)
    {
        var state = await db.KnowledgeIndexStates.AsNoTracking().SingleOrDefaultAsync(x => x.ArticleId == articleId, ct);
        var job = state?.RequestedJobId is Guid id
            ? await db.KnowledgeIndexJobs.AsNoTracking().SingleAsync(x => x.Id == id, ct) : null;
        var count = state?.ActiveIndexSetId is Guid active
            ? await db.KnowledgeIndexSets.Where(x => x.Id == active).Select(x => x.ChunkCount).SingleAsync(ct) : 0;
        return new(articleId, job?.Id, state?.ActiveIndexSetId, job?.Status ?? "Inactive",
            job?.VersionNumber, job?.Attempts ?? 0, job?.ErrorCode, count);
    }
}
