using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Domain.Knowledge;
using ServiceDesk.Infrastructure.Knowledge;
using ServiceDesk.Infrastructure.Persistence;
using ServiceDesk.Infrastructure.Ticketing;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Unit")]
public sealed class PersistenceContractTests
{
    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused").Options);

    [Fact]
    public void CurrentModelMatchesReleasedMigrationSnapshot()
    {
        using var context = Context();
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task NullScopeNeverBecomesUnrestrictedQuery()
    {
        await using var context = Context();
        await Assert.ThrowsAsync<ArgumentException>(() => new PostgresTicketQueries(context)
            .GetByIdAsync(Guid.NewGuid(), null!, default));
        Assert.Throws<ArgumentException>(() => new TicketReadScope.OwnTickets(Guid.Empty));
    }

    [Fact]
    public void KnowledgeModelConfiguredWithExpectedTablesAndConcurrency()
    {
        using var context = Context();
        var articleEntity = context.Model.FindEntityType(typeof(Article));
        Assert.NotNull(articleEntity);
        Assert.Equal("articles", articleEntity.GetTableName());
        Assert.NotNull(articleEntity.FindProperty("xmin"));
        Assert.True(articleEntity.FindProperty("xmin")!.IsConcurrencyToken);

        var tagEntity = context.Model.FindEntityType(typeof(ArticleTag));
        Assert.NotNull(tagEntity);
        Assert.Equal("article_tags", tagEntity.GetTableName());
        Assert.Equal(2, tagEntity.FindPrimaryKey()!.Properties.Count);

        var versionEntity = context.Model.FindEntityType(typeof(ArticleVersion));
        Assert.NotNull(versionEntity);
        Assert.Equal("article_versions", versionEntity.GetTableName());
        Assert.NotNull(versionEntity.FindPrimaryKey());
    }

    [Fact]
    public async Task KnowledgeNullScopeThrowsArgumentException()
    {
        await using var context = Context();
        await Assert.ThrowsAsync<ArgumentException>(() => new PostgresKnowledgeQueries(context)
            .GetByIdAsync(Guid.NewGuid(), null!, default));
    }
}
