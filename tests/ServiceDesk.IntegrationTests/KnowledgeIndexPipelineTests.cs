using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ServiceDesk.Application;
using ServiceDesk.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using ServiceDesk.Application.Common;
using ServiceDesk.Application.Knowledge.Indexing;
using ServiceDesk.Domain.Knowledge;
using ServiceDesk.Infrastructure.Knowledge;
using ServiceDesk.Infrastructure.Knowledge.Indexing;
using ServiceDesk.Infrastructure.Persistence;
using ServiceDesk.Infrastructure.Options;
using Testcontainers.PostgreSql;
using Xunit;

namespace ServiceDesk.IntegrationTests;

public sealed class KnowledgeIndexFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(PostgresTestImages.Pgvector)
        .WithDatabase("knowledge_index_test").WithUsername("test").WithPassword("test").Build();
    public string ConnectionString => _postgres.GetConnectionString();
    public ApplicationDbContext Context(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql(ConnectionString).AddInterceptors(interceptors).Options);
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = Context();
        await db.Database.MigrateAsync();
    }
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
}

[Trait("Category", "Integration")]
public sealed class KnowledgeIndexPipelineTests(KnowledgeIndexFixture fixture)
    : IClassFixture<KnowledgeIndexFixture>, IAsyncLifetime
{
    private readonly DateTime _now = DateTime.UtcNow;
    public async Task InitializeAsync()
    {
        // This fixture owns an isolated, disposable Testcontainers database only.
        await using var db = fixture.Context();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE public.articles CASCADE");
    }
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> PublishAsync()
    {
        await using var db = fixture.Context();
        var writer = new PostgresKnowledgeWriter(db);
        var article = Article.CreateDraft(Guid.NewGuid(), "VPN guide", "First paragraph for indexing.\n\nSecond paragraph for indexing.", null, _now);
        await writer.CreateDraftAsync(article);
        var revision = writer.GetRevision(article);
        article.Publish(article.AuthorId, _now);
        await writer.UpdateArticleAsync(article, revision);
        return article.Id;
    }

    private async Task<KnowledgeIndexLease?> ClaimAsync(DateTime? now = null, int maxAttempts = 5)
    {
        await using var db = fixture.Context();
        return await new PostgresKnowledgeIndexStore(db).ClaimAsync(now ?? _now, TimeSpan.FromSeconds(10), maxAttempts, default);
    }

    private async Task<bool> CompleteAsync(KnowledgeIndexLease lease, DateTime? now = null, bool split = false)
    {
        await using var db = fixture.Context();
        return await new PostgresKnowledgeIndexStore(db).CompleteAsync(lease,
            KnowledgeIndexTestData.Result(lease.Snapshot, split), now ?? _now.AddSeconds(1), default);
    }

    private async Task<KnowledgeIndexStatusDto> StatusAsync(Guid articleId)
    {
        await using var db = fixture.Context();
        return await new PostgresKnowledgeIndexStore(db).GetStatusAsync(articleId, default);
    }

    private async Task<int> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task RepublishAsync(Guid id)
    {
        await using var db = fixture.Context();
        var writer = new PostgresKnowledgeWriter(db);
        var article = (await writer.GetForUpdateAsync(id))!;
        var revision = writer.GetRevision(article);
        article.Republish("VPN updated", "New second version content for indexing.", null, article.AuthorId, _now.AddSeconds(1));
        await writer.UpdateArticleAsync(article, revision);
    }

    private async Task ArchiveAsync(Guid id)
    {
        await using var db = fixture.Context();
        var writer = new PostgresKnowledgeWriter(db);
        var article = (await writer.GetForUpdateAsync(id))!;
        var revision = writer.GetRevision(article);
        article.Archive(_now.AddSeconds(2));
        await writer.UpdateArticleAsync(article, revision);
    }

    [Fact]
    public async Task PublishCreatesDurableJobAndStoresRealPgvectorWithSourceVersion()
    {
        var id = await PublishAsync();
        var before = await StatusAsync(id);
        Assert.Equal("Pending", before.Status);
        Assert.Null(before.ActiveIndexSetId);
        var lease = (await ClaimAsync())!;
        Assert.Equal(id, lease.Snapshot.ArticleId);
        Assert.True(await CompleteAsync(lease));
        var after = await StatusAsync(id);
        Assert.Equal("Succeeded", after.Status);
        Assert.Equal(1, after.ChunkCount);
        Assert.Equal(1, await ScalarAsync("SELECT count(*) FROM public.knowledge_active_chunks"));
        Assert.Equal(384, await ScalarAsync("SELECT vector_dims(\"Embedding\") FROM public.knowledge_chunk_vectors"));
        Assert.False(await CompleteAsync(lease));
        Assert.Equal(1, await ScalarAsync("SELECT count(*) FROM public.knowledge_chunk_vectors"));
    }

    [Fact]
    public async Task RepublishPreservesSnapshotAndInvalidatesOldIndexBeforeV2Completes()
    {
        var id = await PublishAsync();
        var v1 = (await ClaimAsync())!;
        Assert.True(await CompleteAsync(v1));
        await RepublishAsync(id);
        Assert.Null((await StatusAsync(id)).ActiveIndexSetId);
        Assert.Equal(0, await ScalarAsync("SELECT count(*) FROM public.knowledge_active_chunks"));
        var v2 = (await ClaimAsync(_now.AddSeconds(2)))!;
        Assert.Equal(2, v2.Snapshot.VersionNumber);
        Assert.NotEqual(v1.Snapshot.ArticleVersionId, v2.Snapshot.ArticleVersionId);
        Assert.True(await CompleteAsync(v2, _now.AddSeconds(3)));
        await using var db = fixture.Context();
        var versions = await db.ArticleVersions.Where(x => x.ArticleId == id).OrderBy(x => x.VersionNumber).ToListAsync();
        Assert.Equal(v1.Snapshot.Content, versions[0].Content);
        Assert.Equal(2, versions.Count);
        Assert.Equal(2, await db.KnowledgeIndexSets.CountAsync());
        Assert.Equal(2, await ScalarAsync("SELECT \"VersionNumber\" FROM public.knowledge_active_chunks"));
    }

    [Fact]
    public async Task LateV1WorkerCannotActivateAfterV2Publish()
    {
        var id = await PublishAsync();
        var old = (await ClaimAsync())!;
        await RepublishAsync(id);
        Assert.False(await CompleteAsync(old, _now.AddSeconds(2)));
        var current = (await ClaimAsync(_now.AddSeconds(2)))!;
        Assert.True(await CompleteAsync(current, _now.AddSeconds(3)));
        Assert.Equal(2, (await StatusAsync(id)).VersionNumber);
        Assert.Equal(1, await ScalarAsync("SELECT count(*) FROM public.knowledge_chunk_vectors"));
    }

    [Fact]
    public async Task ArchivePreventsLateActivationAndRetainsHistory()
    {
        var id = await PublishAsync();
        var old = (await ClaimAsync())!;
        await ArchiveAsync(id);
        Assert.False(await CompleteAsync(old, _now.AddSeconds(3)));
        Assert.Equal(0, await ScalarAsync("SELECT count(*) FROM public.knowledge_active_chunks"));
        var another = await PublishAsync();
        Assert.True(await CompleteAsync((await ClaimAsync())!));
        await ArchiveAsync(another);
        Assert.Equal(0, await ScalarAsync("SELECT count(*) FROM public.knowledge_active_chunks"));
        Assert.Equal(1, await ScalarAsync("SELECT count(*) FROM public.knowledge_chunk_vectors"));
    }

    [Fact]
    public async Task ProviderFailureRetriesWithNewLeaseAndDoesNotUndoPublish()
    {
        var id = await PublishAsync();
        var first = (await ClaimAsync())!;
        await using (var db = fixture.Context())
            await new PostgresKnowledgeIndexStore(db).FailAsync(first, "index_provider_timeout", true, _now, 5, default);
        Assert.Null(await ClaimAsync(_now.AddSeconds(1)));
        var retry = (await ClaimAsync(_now.AddSeconds(3)))!;
        Assert.Equal(first.JobId, retry.JobId);
        Assert.NotEqual(first.LeaseToken, retry.LeaseToken);
        Assert.False(await CompleteAsync(first, _now.AddSeconds(4)));
        Assert.True(await CompleteAsync(retry, _now.AddSeconds(4)));
        await using var verify = fixture.Context();
        Assert.Equal(ArticleStatus.Published, (await verify.Articles.SingleAsync(x => x.Id == id)).Status);
    }

    [Fact]
    public async Task ExpiredLeaseIsRecoveredButExhaustedAttemptsBecomeTerminal()
    {
        var id = await PublishAsync();
        var first = (await ClaimAsync(maxAttempts: 2))!;
        var second = (await ClaimAsync(_now.AddSeconds(11), 2))!;
        Assert.NotEqual(first.LeaseToken, second.LeaseToken);
        Assert.False(await CompleteAsync(first, _now.AddSeconds(12)));
        Assert.Null(await ClaimAsync(_now.AddSeconds(22), 2));
        Assert.Equal("Failed", (await StatusAsync(id)).Status);
        Assert.Equal("lease_attempts_exhausted", (await StatusAsync(id)).ErrorCode);
    }

    [Fact]
    public async Task ConcurrentWorkersClaimOneJobOnlyOnce()
    {
        await PublishAsync();
        var leases = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ClaimAsync()));
        Assert.Single(leases, x => x is not null);
    }

    [Fact]
    public async Task ConcurrentRepublishHasOneWinnerAndOneVersionSnapshot()
    {
        var id = await PublishAsync();
        await using var left = fixture.Context();
        await using var right = fixture.Context();
        var writerA = new PostgresKnowledgeWriter(left);
        var writerB = new PostgresKnowledgeWriter(right);
        var a = (await writerA.GetForUpdateAsync(id))!;
        var b = (await writerB.GetForUpdateAsync(id))!;
        var revision = writerA.GetRevision(a);
        a.Republish("A", "New content by author A.", null, a.AuthorId, _now.AddSeconds(1));
        b.Republish("B", "New content by author B.", null, b.AuthorId, _now.AddSeconds(1));
        async Task<bool> Save(PostgresKnowledgeWriter writer, Article article)
        {
            try { await writer.UpdateArticleAsync(article, revision); return true; }
            catch (ConcurrencyConflictException) { return false; }
        }
        var results = await Task.WhenAll(Save(writerA, a), Save(writerB, b));
        Assert.Single(results, x => x);
        await using var verify = fixture.Context();
        Assert.Equal(2, await verify.ArticleVersions.CountAsync());
        Assert.Equal(2, await verify.KnowledgeIndexJobs.CountAsync());
    }

    [Fact]
    public async Task RebuildIsIdempotentAndKeepsEligibleOldSetUntilAtomicReplacement()
    {
        var id = await PublishAsync();
        var first = (await ClaimAsync())!;
        Assert.True(await CompleteAsync(first));
        var requestId = Guid.NewGuid();
        await using (var db = fixture.Context())
        {
            var article = await db.Articles.SingleAsync(x => x.Id == id);
            var revision = db.Entry(article).Property<uint>("xmin").CurrentValue;
            var store = new PostgresKnowledgeIndexStore(db);
            Assert.Equal(requestId, await store.RequestRebuildAsync(id, requestId, revision, _now.AddSeconds(2), default));
            Assert.Equal(requestId, await store.RequestRebuildAsync(id, requestId, revision, _now.AddSeconds(2), default));
        }
        Assert.Equal(first.JobId, (await StatusAsync(id)).ActiveIndexSetId);
        var rebuild = (await ClaimAsync(_now.AddSeconds(3)))!;
        Assert.True(await CompleteAsync(rebuild, _now.AddSeconds(4)));
        Assert.Equal(requestId, (await StatusAsync(id)).ActiveIndexSetId);
        Assert.Equal(1, await ScalarAsync("SELECT count(*) FROM public.knowledge_active_chunks"));
        Assert.Equal(2, await ScalarAsync("SELECT count(*) FROM public.knowledge_chunk_vectors"));
        Assert.Equal(1, await ScalarAsync("SELECT count(DISTINCT \"ChunkId\") FROM public.knowledge_chunks"));
    }

    [Fact]
    public async Task VectorWriteFailureRollsBackEveryChunkAndActivation()
    {
        await PublishAsync();
        var lease = (await ClaimAsync())!;
        await using (var db = fixture.Context())
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE public.knowledge_chunk_vectors ADD CONSTRAINT test_fail_second CHECK (\"ChunkIndex\" = 0)");
        try
        {
            await Assert.ThrowsAsync<PostgresException>(() => CompleteAsync(lease, split: true));
            Assert.Equal(0, await ScalarAsync("SELECT count(*) FROM public.knowledge_chunks"));
            Assert.Equal(0, await ScalarAsync("SELECT count(*) FROM public.knowledge_index_sets"));
            Assert.Equal(0, await ScalarAsync("SELECT count(*) FROM public.knowledge_chunk_vectors"));
        }
        finally
        {
            await using var db = fixture.Context();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE public.knowledge_chunk_vectors DROP CONSTRAINT test_fail_second");
        }
        Assert.True(await CompleteAsync(lease, split: true));
        Assert.Equal(2, await ScalarAsync("SELECT count(*) FROM public.knowledge_chunk_vectors"));
    }

    [Fact]
    public async Task JobWriteFailureRollsBackPublishAndVersionSnapshot()
    {
        Guid id;
        await using (var db = fixture.Context())
        {
            var article = Article.CreateDraft(Guid.NewGuid(), "Draft", "Content must survive failure.", null, _now);
            await new PostgresKnowledgeWriter(db).CreateDraftAsync(article);
            id = article.Id;
        }
        await using (var db = fixture.Context(new RejectJobWrite()))
        {
            var writer = new PostgresKnowledgeWriter(db);
            var article = (await writer.GetForUpdateAsync(id))!;
            var revision = writer.GetRevision(article);
            article.Publish(article.AuthorId, _now);
            await Assert.ThrowsAsync<InvalidOperationException>(() => writer.UpdateArticleAsync(article, revision));
        }
        await using var verify = fixture.Context();
        Assert.Equal(ArticleStatus.Draft, (await verify.Articles.SingleAsync()).Status);
        Assert.Empty(await verify.ArticleVersions.ToListAsync());
        Assert.Empty(await verify.KnowledgeIndexJobs.ToListAsync());
    }

    [Fact]
    public async Task UpgradeFromKnowledgeMigrationPreservesExistingPublishedSnapshotAndAllowsRebuild()
    {
        var name = "upgrade_" + Guid.NewGuid().ToString("N");
        await using (var connection = new NpgsqlConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
            await command.ExecuteNonQueryAsync();
        }
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = name }.ConnectionString;
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).Options);
        await db.GetService<IMigrator>().MigrateAsync("20261104100000_AddKnowledgeArticles");
        var article = Article.CreateDraft(Guid.NewGuid(), "Legacy", "Existing published content.", null, _now);
        article.Publish(article.AuthorId, _now);
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        var existing = await db.Articles.SingleAsync();
        Assert.Equal("Existing published content.", existing.Content);
        var revision = db.Entry(existing).Property<uint>("xmin").CurrentValue;
        var store = new PostgresKnowledgeIndexStore(db);
        await store.RequestRebuildAsync(existing.Id, Guid.NewGuid(), revision, _now, default);
        db.ChangeTracker.Clear();
        var lease = (await store.ClaimAsync(_now, TimeSpan.FromSeconds(30), 5, default))!;
        Assert.True(await store.CompleteAsync(lease, KnowledgeIndexTestData.Result(lease.Snapshot), _now.AddSeconds(1), default));
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private sealed class RejectJobWrite : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<KnowledgeIndexJob>().Any(x => x.State == EntityState.Added))
                throw new InvalidOperationException("synthetic_job_write_failure");
            return ValueTask.FromResult(result);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkerProcessorResolvesWithoutHttpContextAndPersistsOutcome(bool providerFails)
    {
        var id = await PublishAsync();
        var services = new ServiceCollection();
        services.AddKnowledgeApplication();
        services.AddScoped(_ => fixture.Context());
        services.AddScoped<IKnowledgeIndexStore, PostgresKnowledgeIndexStore>();
        services.AddSingleton<IClock>(new TestClock(_now.AddSeconds(1)));
        // Smoke tùy chọn dùng AI thật trên localhost; database vẫn là container riêng của test.
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        var endpoint = Environment.GetEnvironmentVariable("KNOWLEDGE_SMOKE_AI_URL");
        IKnowledgeIndexPreparer preparer = new TestPreparer(providerFails);
        if (!providerFails && !string.IsNullOrWhiteSpace(endpoint))
        {
            Assert.True(Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.IsLoopback,
                "Smoke model thật chỉ được gọi AI localhost.");
            preparer = new HttpKnowledgeIndexPreparer(http, Options.Create(new KnowledgeIndexOptions
            {
                Enabled = true, Endpoint = endpoint,
                BearerToken = Environment.GetEnvironmentVariable("KNOWLEDGE_SMOKE_AI_TOKEN") ?? ""
            }));
        }
        services.AddSingleton(preparer);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IKnowledgeIndexProcessor>()
            .ProcessNextAsync(TimeSpan.FromSeconds(30), 5, default);
        Assert.True(result.Found);
        Assert.Equal(providerFails ? "synthetic_provider_unavailable" : "succeeded", result.Outcome);
        var status = await StatusAsync(id);
        Assert.Equal(providerFails ? "Pending" : "Succeeded", status.Status);
        Assert.Equal(providerFails ? 0 : 1, status.ChunkCount);
    }

    private sealed class TestClock(DateTime now) : IClock
    {
        public DateTimeOffset UtcNow => new(now);
    }

    private sealed class TestPreparer(bool fails) : IKnowledgeIndexPreparer
    {
        public Task<PreparedKnowledgeIndex> PrepareAsync(KnowledgeIndexSnapshot snapshot, CancellationToken ct) =>
            fails ? throw new KnowledgeIndexException("synthetic_provider_unavailable", true)
                : Task.FromResult(KnowledgeIndexTestData.Result(snapshot));
    }
}

internal static class KnowledgeIndexTestData
{
    internal static PreparedKnowledgeIndex Result(KnowledgeIndexSnapshot snapshot, bool split = false)
    {
        var source = KnowledgeIndexProfile.Normalize(snapshot.Content);
        var runes = source.EnumerateRunes().Select(x => x.ToString()).ToArray();
        var ends = split ? new[] { runes.Length / 2, runes.Length } : [runes.Length];
        var chunks = new List<PreparedKnowledgeChunk>();
        var start = 0;
        foreach (var end in ends)
        {
            var text = string.Concat(runes[start..end]);
            var hash = KnowledgeIndexProfile.Hash(text);
            var vector = new float[384]; vector[0] = 1;
            chunks.Add(new(KnowledgeIndexProfile.ChunkId(snapshot.ArticleVersionId, snapshot.SourceHash, chunks.Count, hash),
                chunks.Count, text, hash, start, end,
                1 + runes[..start].Count(x => x == "\n"), 1 + runes[..(end - 1)].Count(x => x == "\n"), [], 10, vector));
            start = end;
        }
        return new(snapshot.RequestId, snapshot.ArticleId, snapshot.ArticleVersionId, snapshot.VersionNumber,
            snapshot.SourceHash, KnowledgeIndexProfile.Id, KnowledgeIndexProfile.Fingerprint,
            KnowledgeIndexProfile.Hash(source), chunks);
    }
}
