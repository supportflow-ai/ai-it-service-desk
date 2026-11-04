using FluentAssertions;
using ServiceDesk.Domain.Knowledge;
using Xunit;

namespace ServiceDesk.Domain.Tests.Knowledge;

public class ArticleTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = new(2026, 11, 4, 11, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ArchiveTime = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateDraft_ValidInputs_InitializesDraftArticle()
    {
        var authorId = Guid.NewGuid();
        var tags = new[] { " IT ", "SUPPORT", "it" };

        var article = Article.CreateDraft(
            authorId,
            "  How to reset VPN password  ",
            "  Follow these steps to reset your corporate VPN password.  ",
            tags,
            Now);

        article.Id.Should().NotBeEmpty();
        article.AuthorId.Should().Be(authorId);
        article.Title.Should().Be("How to reset VPN password");
        article.Content.Should().Be("Follow these steps to reset your corporate VPN password.");
        article.Status.Should().Be(ArticleStatus.Draft);
        article.Version.Should().Be(1);
        article.CreatedAt.Should().Be(Now);
        article.UpdatedAt.Should().Be(Now);
        article.PublishedAt.Should().BeNull();
        article.ArchivedAt.Should().BeNull();
        article.Tags.Should().HaveCount(2);
        article.Tags.Select(t => t.Name).Should().ContainInOrder("it", "support");
        article.Tags.All(t => t.ArticleId == article.Id).Should().BeTrue();
        article.Versions.Should().BeEmpty();
    }

    [Fact]
    public void CreateDraft_NullOrEmptyTags_Allowed()
    {
        var authorId = Guid.NewGuid();

        var articleWithNullTags = Article.CreateDraft(authorId, "Valid Title", "Valid Content at least 10 chars", null, Now);
        articleWithNullTags.Tags.Should().BeEmpty();

        var articleWithEmptyTags = Article.CreateDraft(authorId, "Valid Title", "Valid Content at least 10 chars", [], Now);
        articleWithEmptyTags.Tags.Should().BeEmpty();
    }

    [Fact]
    public void CreateDraft_RejectsEmptyAuthorId()
    {
        var act = () => Article.CreateDraft(
            Guid.Empty,
            "Valid Title",
            "Valid Content at least 10 chars",
            null,
            Now);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Author ID is required.*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateDraft_RejectsBlankTitle(string? title)
    {
        var act = () => Article.CreateDraft(
            Guid.NewGuid(),
            title!,
            "Valid Content at least 10 chars",
            null,
            Now);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Title is required.*");
    }

    [Fact]
    public void CreateDraft_RejectsTitleLongerThan200Characters()
    {
        var longTitle = new string('A', 201);
        var act = () => Article.CreateDraft(
            Guid.NewGuid(),
            longTitle,
            "Valid Content at least 10 chars",
            null,
            Now);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Title must not exceed 200 characters.*");
    }

    [Fact]
    public void CreateDraft_AcceptsTitleExactly200Characters()
    {
        var exactTitle = new string('A', 200);
        var article = Article.CreateDraft(
            Guid.NewGuid(),
            exactTitle,
            "Valid Content at least 10 chars",
            null,
            Now);

        article.Title.Length.Should().Be(200);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateDraft_RejectsBlankContent(string? content)
    {
        var act = () => Article.CreateDraft(
            Guid.NewGuid(),
            "Valid Title",
            content!,
            null,
            Now);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Content is required.*");
    }

    [Theory]
    [InlineData("1")]
    [InlineData("123456789")]
    [InlineData("  123456789  ")]
    public void CreateDraft_RejectsContentShorterThan10Characters(string shortContent)
    {
        var act = () => Article.CreateDraft(
            Guid.NewGuid(),
            "Valid Title",
            shortContent,
            null,
            Now);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Content must be at least 10 characters.*");
    }

    [Fact]
    public void CreateDraft_AcceptsContentExactly10Characters()
    {
        var article = Article.CreateDraft(
            Guid.NewGuid(),
            "Valid Title",
            "1234567890",
            null,
            Now);

        article.Content.Should().Be("1234567890");
    }

    [Fact]
    public void CreateDraft_RejectsMoreThan10DistinctTags()
    {
        var tags = Enumerable.Range(1, 11).Select(i => $"tag{i}").ToArray();

        var act = () => Article.CreateDraft(
            Guid.NewGuid(),
            "Valid Title",
            "Valid Content at least 10 chars",
            tags,
            Now);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Article cannot have more than 10 tags.*");
    }

    [Fact]
    public void CreateDraft_RejectsTagLongerThan50Characters()
    {
        var tags = new[] { new string('a', 51) };

        var act = () => Article.CreateDraft(
            Guid.NewGuid(),
            "Valid Title",
            "Valid Content at least 10 chars",
            tags,
            Now);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Tag must not exceed 50 characters.*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateDraft_RejectsEmptyOrWhitespaceTag(string invalidTag)
    {
        var tags = new[] { "valid", invalidTag };

        var act = () => Article.CreateDraft(
            Guid.NewGuid(),
            "Valid Title",
            "Valid Content at least 10 chars",
            tags,
            Now);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Tag cannot be empty or whitespace.*");
    }

    [Fact]
    public void UpdateDraft_ValidInputs_UpdatesArticleAndReplacesTags()
    {
        var article = CreateValidDraft();

        article.UpdateDraft(
            "  Updated Title  ",
            "  Updated content that meets requirement.  ",
            new[] { "security", "vpn" },
            Later);

        article.Title.Should().Be("Updated Title");
        article.Content.Should().Be("Updated content that meets requirement.");
        article.UpdatedAt.Should().Be(Later);
        article.Tags.Should().HaveCount(2);
        article.Tags.Select(t => t.Name).Should().ContainInOrder("security", "vpn");
        article.Tags.All(t => t.ArticleId == article.Id).Should().BeTrue();
    }

    [Fact]
    public void UpdateDraft_RejectsWhenNotDraft()
    {
        var publishedArticle = CreateValidDraft();
        publishedArticle.Publish(Guid.NewGuid(), Later);

        var actPublished = () => publishedArticle.UpdateDraft("New Title", "New content 10 chars", null, ArchiveTime);
        actPublished.Should().Throw<InvalidOperationException>()
            .WithMessage("Chỉ có thể chỉnh sửa bài viết ở trạng thái nháp.");

        var archivedArticle = CreateValidDraft();
        archivedArticle.Publish(Guid.NewGuid(), Later);
        archivedArticle.Archive(ArchiveTime);

        var actArchived = () => archivedArticle.UpdateDraft("New Title", "New content 10 chars", null, ArchiveTime);
        actArchived.Should().Throw<InvalidOperationException>()
            .WithMessage("Chỉ có thể chỉnh sửa bài viết ở trạng thái nháp.");
    }

    [Fact]
    public void UpdateDraft_ValidatesInvariants()
    {
        var article = CreateValidDraft();

        var actInvalidTitle = () => article.UpdateDraft("", "Valid content 10 chars", null, Later);
        actInvalidTitle.Should().Throw<ArgumentException>();

        var actInvalidContent = () => article.UpdateDraft("Valid Title", "short", null, Later);
        actInvalidContent.Should().Throw<ArgumentException>();

        var actInvalidTags = () => article.UpdateDraft("Valid Title", "Valid content 10 chars", Enumerable.Range(1, 11).Select(i => $"t{i}"), Later);
        actInvalidTags.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Publish_FromDraft_TransitionsToPublishedAndCreatesVersionSnapshot()
    {
        var article = CreateValidDraft();
        var publisherId = Guid.NewGuid();

        article.Publish(publisherId, Later);

        article.Status.Should().Be(ArticleStatus.Published);
        article.PublishedAt.Should().Be(Later);
        article.UpdatedAt.Should().Be(Later);
        article.Versions.Should().HaveCount(1);

        var snapshot = article.Versions.First();
        snapshot.Id.Should().NotBeEmpty();
        snapshot.ArticleId.Should().Be(article.Id);
        snapshot.VersionNumber.Should().Be(1);
        snapshot.Title.Should().Be(article.Title);
        snapshot.Content.Should().Be(article.Content);
        snapshot.PublishedById.Should().Be(publisherId);
        snapshot.PublishedAt.Should().Be(Later);
    }

    [Fact]
    public void Publish_RejectsEmptyPublishedById()
    {
        var article = CreateValidDraft();

        var act = () => article.Publish(Guid.Empty, Later);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*PublishedBy ID is required.*");
    }

    [Fact]
    public void Publish_RejectsWhenNotDraft()
    {
        var article = CreateValidDraft();
        article.Publish(Guid.NewGuid(), Later);

        var actAlreadyPublished = () => article.Publish(Guid.NewGuid(), ArchiveTime);
        actAlreadyPublished.Should().Throw<InvalidOperationException>()
            .WithMessage("Chỉ có thể xuất bản bài viết từ trạng thái nháp.");

        article.Archive(ArchiveTime);
        var actArchived = () => article.Publish(Guid.NewGuid(), ArchiveTime);
        actArchived.Should().Throw<InvalidOperationException>()
            .WithMessage("Chỉ có thể xuất bản bài viết từ trạng thái nháp.");
    }

    [Fact]
    public void Archive_FromPublished_TransitionsToArchived()
    {
        var article = CreateValidDraft();
        article.Publish(Guid.NewGuid(), Later);

        article.Archive(ArchiveTime);

        article.Status.Should().Be(ArticleStatus.Archived);
        article.ArchivedAt.Should().Be(ArchiveTime);
        article.UpdatedAt.Should().Be(ArchiveTime);
    }

    [Fact]
    public void Archive_RejectsWhenNotPublished()
    {
        var draftArticle = CreateValidDraft();
        var actDraft = () => draftArticle.Archive(Later);
        actDraft.Should().Throw<InvalidOperationException>()
            .WithMessage("Chỉ có thể lưu trữ bài viết đang ở trạng thái đã xuất bản.");

        var archivedArticle = CreateValidDraft();
        archivedArticle.Publish(Guid.NewGuid(), Later);
        archivedArticle.Archive(ArchiveTime);

        var actAlreadyArchived = () => archivedArticle.Archive(ArchiveTime.AddHours(1));
        actAlreadyArchived.Should().Throw<InvalidOperationException>()
            .WithMessage("Chỉ có thể lưu trữ bài viết đang ở trạng thái đã xuất bản.");
    }

    [Fact]
    public void IsAuthor_ChecksAuthorCorrectly()
    {
        var authorId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var article = Article.CreateDraft(authorId, "Title here", "Content that is 10 chars", null, Now);

        article.IsAuthor(authorId).Should().BeTrue();
        article.IsAuthor(otherUserId).Should().BeFalse();
    }

    [Fact]
    public void ArticleTag_ValidatesInvariants()
    {
        var articleId = Guid.NewGuid();

        var actEmptyArticleId = () => new ArticleTag(Guid.Empty, "tag");
        actEmptyArticleId.Should().Throw<ArgumentException>();

        var actEmptyName = () => new ArticleTag(articleId, "   ");
        actEmptyName.Should().Throw<ArgumentException>();

        var actTooLongName = () => new ArticleTag(articleId, new string('x', 51));
        actTooLongName.Should().Throw<ArgumentException>();

        var validTag = new ArticleTag(articleId, "  TAG-name  ");
        validTag.Name.Should().Be("tag-name");
        validTag.ArticleId.Should().Be(articleId);
    }

    [Fact]
    public void ArticleVersion_ValidatesInvariants()
    {
        var versionId = Guid.NewGuid();
        var articleId = Guid.NewGuid();
        var publisherId = Guid.NewGuid();

        var actEmptyId = () => new ArticleVersion(Guid.Empty, articleId, 1, "Title", "Content 10 chars", publisherId, Now);
        actEmptyId.Should().Throw<ArgumentException>();

        var actEmptyArticleId = () => new ArticleVersion(versionId, Guid.Empty, 1, "Title", "Content 10 chars", publisherId, Now);
        actEmptyArticleId.Should().Throw<ArgumentException>();

        var actZeroVersion = () => new ArticleVersion(versionId, articleId, 0, "Title", "Content 10 chars", publisherId, Now);
        actZeroVersion.Should().Throw<ArgumentException>();

        var actEmptyTitle = () => new ArticleVersion(versionId, articleId, 1, "  ", "Content 10 chars", publisherId, Now);
        actEmptyTitle.Should().Throw<ArgumentException>();

        var actEmptyContent = () => new ArticleVersion(versionId, articleId, 1, "Title", "  ", publisherId, Now);
        actEmptyContent.Should().Throw<ArgumentException>();

        var actEmptyPublisher = () => new ArticleVersion(versionId, articleId, 1, "Title", "Content 10 chars", Guid.Empty, Now);
        actEmptyPublisher.Should().Throw<ArgumentException>();

        var version = new ArticleVersion(versionId, articleId, 2, "  Title  ", "  Content 10 chars  ", publisherId, Now);
        version.Title.Should().Be("Title");
        version.Content.Should().Be("Content 10 chars");
        version.VersionNumber.Should().Be(2);
    }

    private static Article CreateValidDraft()
    {
        return Article.CreateDraft(
            Guid.NewGuid(),
            "Default Guide Title",
            "This is default article content for testing.",
            new[] { "default", "guide" },
            Now);
    }
}
