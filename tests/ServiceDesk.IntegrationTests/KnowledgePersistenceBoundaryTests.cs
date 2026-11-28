using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Knowledge.Application.Authorization;
using ServiceDesk.Modules.Knowledge.Domain;
using ServiceDesk.Modules.Knowledge.Application.Ports;
using ServiceDesk.Modules.Knowledge.Infrastructure;
using ServiceDesk.SharedKernel.Application;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class KnowledgePersistenceBoundaryTests(TicketIdempotencyPostgresFixture fixture) : IClassFixture<TicketIdempotencyPostgresFixture>
{
    [Theory]
    [InlineData("draft")]
    [InlineData("publish")]
    [InlineData("republish")]
    public async Task ConcurrentWritesKeepOneWinnerAndPreservePublishedHistory(string operation)
    {
        var author = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var article = Article.CreateDraft(author, "Original title", "Original content", new[] { "Original" }, now);
        if (operation == "republish") article.Publish(author, now);
        await using (var setup = fixture.CreateContext()) { setup.Set<Article>().Add(article); await setup.SaveChangesAsync(); }
        await using var first = fixture.CreateContext();
        await using var second = fixture.CreateContext();
        var writerA = new PostgresKnowledgeWriter(first);
        var writerB = new PostgresKnowledgeWriter(second);
        var a = (await writerA.GetForUpdateAsync(article.Id))!;
        var b = (await writerB.GetForUpdateAsync(article.Id))!;
        void Change(Article value, string title)
        {
            if (operation == "draft") value.UpdateDraft(title, "Changed draft content", new[] { " New ", "new" }, now.AddMinutes(1));
            else if (operation == "publish") value.Publish(author, now.AddMinutes(1));
            else value.Republish(title, "Changed published content", new[] { " New ", "new" }, author, now.AddMinutes(1));
        }
        Change(a, "Winner A"); Change(b, "Winner B");
        async Task<bool> Save(PostgresKnowledgeWriter writer, Article value)
        {
            try { await writer.UpdateArticleAsync(value); return true; }
            catch (ConcurrencyConflictException) { return false; }
        }
        var outcomes = await Task.WhenAll(Save(writerA, a), Save(writerB, b));
        Assert.Single(outcomes, success => success);
        Assert.Single(outcomes, success => !success);
        await using var verify = fixture.CreateContext();
        var saved = (await new PostgresKnowledgeWriter(verify).GetForUpdateAsync(article.Id))!;
        Assert.Equal(operation == "draft" ? 0 : operation == "publish" ? 1 : 2, saved.Versions.Count);
        if (operation == "republish") Assert.Equal("Original content", saved.Versions.Single(v => v.VersionNumber == 1).Content);
        if (operation != "publish") Assert.Equal("new", Assert.Single(saved.Tags).Name);
    }

[Fact]
    public async Task LockedArticleReturnsRetryLaterWithoutWritingTags()
    {
        var article = Article.CreateDraft(Guid.NewGuid(), "Locked article", "Original content", new[] { "original" }, DateTime.UtcNow);
        await using (var setup = fixture.CreateContext()) { setup.Set<Article>().Add(article); await setup.SaveChangesAsync(); }
        await using var blocker = fixture.CreateContext();
        await using var transaction = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM public.articles WHERE \"Id\" = {article.Id} FOR UPDATE");
        await using var contender = fixture.CreateContext();
        var writer = new PostgresKnowledgeWriter(contender);
        var loaded = (await writer.GetForUpdateAsync(article.Id))!;
        loaded.UpdateDraft("New title", "New content", new[] { "changed" }, DateTime.UtcNow);
        var error = await Assert.ThrowsAsync<KnowledgeWriteBusyException>(() => writer.UpdateArticleAsync(loaded));
        Assert.Equal(1, error.RetryAfterSeconds);
        await transaction.RollbackAsync();
        await using var verify = fixture.CreateContext();
        Assert.Equal("original", Assert.Single((await new PostgresKnowledgeWriter(verify).GetForUpdateAsync(article.Id))!.Tags).Name);
    }

    [Fact]
    public async Task VisibilityAndTagsAreAppliedBeforePaginationAndCounting()
    {
        var keyword = $"kb-{Guid.NewGuid():N}";
        var author = Guid.NewGuid();
        var other = Guid.NewGuid();
        await using (var setup = fixture.CreateContext())
        {
            foreach (var owner in new[] { author, other })
            foreach (var status in new[] { ArticleStatus.Draft, ArticleStatus.Published, ArticleStatus.Archived })
            {
                var article = Article.CreateDraft(owner, keyword, "Visibility content", new[] { " Public ", "public", status == ArticleStatus.Draft ? keyword : "Visible" }, DateTime.UtcNow);
                if (status != ArticleStatus.Draft) article.Publish(owner, DateTime.UtcNow);
                if (status == ArticleStatus.Archived) article.Archive(DateTime.UtcNow);
                setup.Set<Article>().Add(article);
            }
            await setup.SaveChangesAsync();
        }
        await using var db = fixture.CreateContext();
        var query = new PostgresKnowledgeQueries(db);
        var publicPage = await query.SearchPublishedAsync(keyword, " PUBLIC ", 1, 1);
        var nextPage = await query.SearchPublishedAsync(keyword, "public", 2, 1);
        Assert.Equal(2, publicPage.TotalCount);
        Assert.Single(publicPage.Items);
        Assert.NotEqual(publicPage.Items[0].Id, Assert.Single(nextPage.Items).Id);
        var own = await query.SearchStaffAsync(keyword, null, null, new KnowledgeReadScope.Author(author), 1, 1);
        Assert.Equal(4, own.TotalCount);
        Assert.Single(own.Items);
        Assert.DoesNotContain(await query.GetTagsAsync(), t => t.Name == keyword);
    }
}
