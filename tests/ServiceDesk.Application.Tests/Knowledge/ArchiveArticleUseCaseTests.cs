using Moq;
using ServiceDesk.Application.Common;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Knowledge.ArchiveArticle;
using ServiceDesk.Application.Knowledge.Ports;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Knowledge;
using Xunit;

namespace ServiceDesk.Application.Tests.Knowledge;

public sealed class ArchiveArticleUseCaseTests
{
    private readonly Mock<IKnowledgeWriter> _writer = new();
    private readonly Mock<ICurrentUser> _user = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Guid _authorId = Guid.NewGuid();
    private readonly Guid _agentId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

    public ArchiveArticleUseCaseTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    [Fact]
    public async Task Archive_WhenUserIsRequester_ThrowsUnauthorizedAccessException()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var useCase = new ArchiveArticleUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new ArchiveArticleCommand(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Chỉ kỹ thuật viên hoặc quản trị viên mới có thể lưu trữ bài viết.", ex.Message);
    }

    [Fact]
    public async Task Archive_WhenUserIsAgent_ArchivesPublishedArticleSuccessfully()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _user.SetupGet(u => u.UserId).Returns(_authorId.ToString());

        var article = Article.CreateDraft(_authorId, "Tiêu đề bài viết", "Nội dung bài viết hợp lệ từ 10 ký tự", ["tag1"], _now.UtcDateTime);
        article.Publish(_agentId, _now.UtcDateTime);

        _writer.Setup(w => w.GetForUpdateAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(article);
        _writer.Setup(w => w.UpdateArticleAsync(article, It.IsAny<uint?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Article a, uint? _, CancellationToken _) => a);
        _writer.Setup(w => w.GetRevision(article)).Returns(3u);

        var useCase = new ArchiveArticleUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new ArchiveArticleCommand(article.Id);

        var result = await useCase.ExecuteAsync(command);

        Assert.NotNull(result);
        Assert.Equal(ArticleStatus.Archived, result.Status);
        Assert.Equal(_now.UtcDateTime, result.ArchivedAt);
        Assert.Equal(3u, result.Revision);
        Assert.Single(result.Tags);
        Assert.Contains("tag1", result.Tags);
        _writer.Verify(w => w.UpdateArticleAsync(article, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Archive_WhenUserIsNonAuthorAgent_ThrowsUnauthorizedAccessException()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _user.SetupGet(u => u.UserId).Returns(_agentId.ToString());

        var article = Article.CreateDraft(_authorId, "Tiêu đề bài viết", "Nội dung bài viết hợp lệ từ 10 ký tự", null, _now.UtcDateTime);
        article.Publish(_authorId, _now.UtcDateTime);

        _writer.Setup(w => w.GetForUpdateAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(article);

        var useCase = new ArchiveArticleUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new ArchiveArticleCommand(article.Id);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Chỉ tác giả hoặc quản trị viên mới có quyền lưu trữ bài viết.", ex.Message);
    }

    [Fact]
    public async Task Archive_WhenArticleDoesNotExist_ThrowsKeyNotFoundException()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _writer.Setup(w => w.GetForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Article?)null);

        var useCase = new ArchiveArticleUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new ArchiveArticleCommand(Guid.NewGuid());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task Archive_WhenRevisionMismatch_ThrowsConcurrencyConflictException()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _user.SetupGet(u => u.UserId).Returns(_authorId.ToString());

        var article = Article.CreateDraft(_authorId, "Tiêu đề bài viết", "Nội dung bài viết hợp lệ từ 10 ký tự", null, _now.UtcDateTime);
        article.Publish(_agentId, _now.UtcDateTime);

        _writer.Setup(w => w.GetForUpdateAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(article);
        _writer.Setup(w => w.GetRevision(article)).Returns(10u);

        var useCase = new ArchiveArticleUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new ArchiveArticleCommand(article.Id, ExpectedRevision: 5u);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task Archive_WhenArticleIsDraft_ThrowsInvalidOperationException()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _user.SetupGet(u => u.UserId).Returns(_authorId.ToString());

        var article = Article.CreateDraft(_authorId, "Tiêu đề bài viết", "Nội dung bài viết hợp lệ từ 10 ký tự", null, _now.UtcDateTime);
        _writer.Setup(w => w.GetForUpdateAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(article);

        var useCase = new ArchiveArticleUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new ArchiveArticleCommand(article.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Chỉ có thể lưu trữ bài viết đang ở trạng thái đã xuất bản.", ex.Message);
    }
}
