using Moq;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Knowledge.Application.Ports;
using ServiceDesk.Modules.Knowledge.Application.PublishArticle;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Knowledge.Domain;
using Xunit;

namespace ServiceDesk.Application.Tests.Knowledge;

public sealed class PublishArticleUseCaseTests
{
    private readonly Mock<IKnowledgeWriter> _writer = new();
    private readonly Mock<ICurrentUser> _user = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Guid _authorId = Guid.NewGuid();
    private readonly Guid _agentId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

    public PublishArticleUseCaseTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    [Fact]
    public async Task Publish_WhenUserIsRequester_ThrowsUnauthorizedAccessException()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var useCase = new PublishArticleUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new PublishArticleCommand(Guid.NewGuid(), ExpectedRevision: 1u);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Chỉ kỹ thuật viên hoặc quản trị viên mới có thể xuất bản bài viết.", ex.Message);
    }

    [Fact]
    public async Task Publish_WhenUserIsAgent_PublishesDraftSuccessfully()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _user.SetupGet(u => u.UserId).Returns(_agentId.ToString());
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);

        var article = Article.CreateDraft(_agentId, "Tiêu đề bài viết", "Nội dung bài viết hợp lệ từ 10 ký tự", ["tag1"], _now.UtcDateTime);
        _writer.Setup(w => w.GetForUpdateAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(article);
        _writer.Setup(w => w.UpdateArticleAsync(article, It.IsAny<uint?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Article a, uint? _, CancellationToken _) => a);
        _writer.SetupSequence(w => w.GetRevision(article)).Returns(1u).Returns(2u);

        var useCase = new PublishArticleUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new PublishArticleCommand(article.Id, ExpectedRevision: 1u);

        var result = await useCase.ExecuteAsync(command);

        Assert.NotNull(result);
        Assert.Equal(ArticleStatus.Published, result.Status);
        Assert.Equal(_now.UtcDateTime, result.PublishedAt);
        Assert.Equal(2u, result.Revision);
        Assert.Single(result.Tags);
        Assert.Contains("tag1", result.Tags);

        Assert.Single(article.Versions);
        var version = article.Versions.First();
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal(_agentId, version.PublishedById);
        Assert.Equal("Tiêu đề bài viết", version.Title);
        Assert.Equal("Nội dung bài viết hợp lệ từ 10 ký tự", version.Content);
        Assert.Equal(_now.UtcDateTime, version.PublishedAt);
    }

    [Fact]
    public async Task Publish_WhenUserIsNonAuthorAgent_ThrowsUnauthorizedAccessException()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _user.SetupGet(u => u.UserId).Returns(_agentId.ToString());
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);

        var article = Article.CreateDraft(_authorId, "Tiêu đề bài viết", "Nội dung bài viết hợp lệ từ 10 ký tự", null, _now.UtcDateTime);
        _writer.Setup(w => w.GetForUpdateAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(article);

        var useCase = new PublishArticleUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new PublishArticleCommand(article.Id, ExpectedRevision: 1u);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Chỉ tác giả hoặc quản trị viên mới có quyền xuất bản bài viết.", ex.Message);
    }

    [Fact]
    public async Task Publish_WhenArticleDoesNotExist_ThrowsKeyNotFoundException()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _writer.Setup(w => w.GetForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Article?)null);

        var useCase = new PublishArticleUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new PublishArticleCommand(Guid.NewGuid(), ExpectedRevision: 1u);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task Publish_WhenRevisionMismatch_ThrowsConcurrencyConflictException()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _user.SetupGet(u => u.UserId).Returns(_agentId.ToString());
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);

        var article = Article.CreateDraft(_agentId, "Tiêu đề bài viết", "Nội dung bài viết hợp lệ từ 10 ký tự", null, _now.UtcDateTime);
        _writer.Setup(w => w.GetForUpdateAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(article);
        _writer.Setup(w => w.GetRevision(article)).Returns(10u);

        var useCase = new PublishArticleUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new PublishArticleCommand(article.Id, ExpectedRevision: 5u);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task Publish_WhenArticleAlreadyPublished_ThrowsInvalidOperationException()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _user.SetupGet(u => u.UserId).Returns(_agentId.ToString());
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);

        var article = Article.CreateDraft(_agentId, "Tiêu đề bài viết", "Nội dung bài viết hợp lệ từ 10 ký tự", null, _now.UtcDateTime);
        article.Publish(_agentId, _now.UtcDateTime);
        _writer.Setup(w => w.GetForUpdateAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(article);
        _writer.Setup(w => w.GetRevision(article)).Returns(1u);

        var useCase = new PublishArticleUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new PublishArticleCommand(article.Id, ExpectedRevision: 1u);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(command));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0u)]
    public async Task Publish_WhenExpectedRevisionMissingOrZero_ThrowsArgumentException(uint? revision)
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _user.SetupGet(u => u.UserId).Returns(_agentId.ToString());
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);

        var article = Article.CreateDraft(_agentId, "Tiêu đề bài viết", "Nội dung bài viết hợp lệ từ 10 ký tự", null, _now.UtcDateTime);
        _writer.Setup(w => w.GetForUpdateAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(article);

        var useCase = new PublishArticleUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new PublishArticleCommand(article.Id, ExpectedRevision: revision);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(command));
        Assert.Equal("Expected revision is required.", ex.Message);
    }
}
