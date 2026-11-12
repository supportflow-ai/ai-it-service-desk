using Moq;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Knowledge.Application.CreateDraftArticle;
using ServiceDesk.Modules.Knowledge.Application.Ports;
using ServiceDesk.Modules.Knowledge.Application.UpdateDraftArticle;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Knowledge.Domain;
using Xunit;

namespace ServiceDesk.Application.Tests.Knowledge;

public sealed class DraftArticleUseCaseTests
{
    private readonly Mock<IKnowledgeWriter> _writer = new();
    private readonly Mock<ICurrentUser> _user = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Guid _authorId = Guid.NewGuid();
    private readonly Guid _otherAgentId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

    public DraftArticleUseCaseTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    [Fact]
    public async Task CreateDraft_WhenUserIsRequester_ThrowsUnauthorizedAccessException()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var useCase = new CreateDraftArticleUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new CreateDraftArticleCommand("Tiêu đề bài viết", "Nội dung bài viết hợp lệ từ 10 ký tự", null);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Chỉ kỹ thuật viên hoặc quản trị viên", ex.Message);
    }

    [Fact]
    public async Task CreateDraft_WhenUserIsAgent_SucceedsAndSetsAuthorId()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _user.SetupGet(u => u.UserId).Returns(_authorId.ToString());
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);

        _writer.Setup(w => w.CreateDraftAsync(It.IsAny<Article>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Article a, CancellationToken _) => a);
        _writer.Setup(w => w.GetRevision(It.IsAny<Article>())).Returns(1u);

        var useCase = new CreateDraftArticleUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new CreateDraftArticleCommand("Tiêu đề bản nháp", "Nội dung chi tiết bài viết hướng dẫn", ["wifi", "network"]);

        var result = await useCase.ExecuteAsync(command);

        Assert.NotNull(result);
        Assert.Equal(_authorId, result.AuthorId);
        Assert.Equal("Tiêu đề bản nháp", result.Title);
        Assert.Equal("Nội dung chi tiết bài viết hướng dẫn", result.Content);
        Assert.Equal(ArticleStatus.Draft, result.Status);
        Assert.Equal(1, result.Version);
        Assert.Equal(1u, result.Revision);
        Assert.Equal(2, result.Tags.Count);
        Assert.Contains("wifi", result.Tags);
        Assert.Contains("network", result.Tags);
    }

    [Fact]
    public async Task UpdateDraft_WhenArticleDoesNotExist_ThrowsKeyNotFoundException()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _writer.Setup(w => w.GetForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Article?)null);

        var useCase = new UpdateDraftArticleUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new UpdateDraftArticleCommand(Guid.NewGuid(), "Tiêu đề mới", "Nội dung bài viết mới cập nhật", ExpectedRevision: 1u);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task UpdateDraft_WhenAnotherAgentTriesToEdit_ThrowsUnauthorizedAccessException()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _user.SetupGet(u => u.UserId).Returns(_otherAgentId.ToString());
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);

        var article = Article.CreateDraft(_authorId, "Tiêu đề gốc", "Nội dung gốc bài viết", null, _now.UtcDateTime);
        _writer.Setup(w => w.GetForUpdateAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(article);

        var useCase = new UpdateDraftArticleUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new UpdateDraftArticleCommand(article.Id, "Tiêu đề bị sửa", "Nội dung cố tình bị sửa", ExpectedRevision: 1u);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Chỉ tác giả hoặc quản trị viên mới có quyền chỉnh sửa bản nháp.", ex.Message);
    }

    [Fact]
    public async Task UpdateDraft_WhenAdminEditsAnotherAgentDraft_Succeeds()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(true);
        _user.SetupGet(u => u.UserId).Returns(_adminId.ToString());
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);

        var article = Article.CreateDraft(_authorId, "Tiêu đề gốc", "Nội dung gốc bài viết", null, _now.UtcDateTime);
        _writer.Setup(w => w.GetForUpdateAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(article);
        _writer.Setup(w => w.UpdateArticleAsync(article, It.IsAny<uint?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Article a, uint? _, CancellationToken _) => a);
        _writer.SetupSequence(w => w.GetRevision(article)).Returns(1u).Returns(2u);

        var useCase = new UpdateDraftArticleUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new UpdateDraftArticleCommand(article.Id, "Tiêu đề Admin sửa", "Nội dung được Admin hiệu chỉnh", ["admin-tag"], ExpectedRevision: 1u);

        var result = await useCase.ExecuteAsync(command);

        Assert.NotNull(result);
        Assert.Equal("Tiêu đề Admin sửa", result.Title);
        Assert.Equal("Nội dung được Admin hiệu chỉnh", result.Content);
        Assert.Equal(2u, result.Revision);
        Assert.Single(result.Tags);
        Assert.Contains("admin-tag", result.Tags);
    }

    [Fact]
    public async Task UpdateDraft_WhenRevisionMismatch_ThrowsConcurrencyConflictException()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _user.SetupGet(u => u.UserId).Returns(_authorId.ToString());
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);

        var article = Article.CreateDraft(_authorId, "Tiêu đề gốc", "Nội dung gốc bài viết", null, _now.UtcDateTime);
        _writer.Setup(w => w.GetForUpdateAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(article);
        _writer.Setup(w => w.GetRevision(article)).Returns(10u);

        var useCase = new UpdateDraftArticleUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new UpdateDraftArticleCommand(article.Id, "Tiêu đề sửa", "Nội dung mới cập nhật", null, ExpectedRevision: 5u);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task UpdateDraft_WhenArticleStatusIsPublished_ThrowsInvalidOperationException()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _user.SetupGet(u => u.UserId).Returns(_authorId.ToString());
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);

        var article = Article.CreateDraft(_authorId, "Tiêu đề gốc", "Nội dung gốc bài viết", null, _now.UtcDateTime);
        article.Publish(_authorId, _now.UtcDateTime);
        _writer.Setup(w => w.GetForUpdateAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(article);
        _writer.Setup(w => w.GetRevision(article)).Returns(1u);

        var useCase = new UpdateDraftArticleUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new UpdateDraftArticleCommand(article.Id, "Tiêu đề mới", "Nội dung mới sau khi xuất bản", ExpectedRevision: 1u);

        await Assert.ThrowsAsync<RuleViolationException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task UpdateDraft_WhenArticleStatusIsArchived_ThrowsInvalidOperationException()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _user.SetupGet(u => u.UserId).Returns(_authorId.ToString());
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);

        var article = Article.CreateDraft(_authorId, "Tiêu đề gốc", "Nội dung gốc bài viết", null, _now.UtcDateTime);
        article.Publish(_authorId, _now.UtcDateTime);
        article.Archive(_now.UtcDateTime);
        _writer.Setup(w => w.GetForUpdateAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(article);
        _writer.Setup(w => w.GetRevision(article)).Returns(1u);

        var useCase = new UpdateDraftArticleUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new UpdateDraftArticleCommand(article.Id, "Tiêu đề mới", "Nội dung mới sau khi lưu trữ", ExpectedRevision: 1u);

        await Assert.ThrowsAsync<RuleViolationException>(() => useCase.ExecuteAsync(command));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0u)]
    public async Task UpdateDraft_WhenExpectedRevisionMissingOrZero_ThrowsArgumentException(uint? revision)
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _user.SetupGet(u => u.UserId).Returns(_authorId.ToString());
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);

        var article = Article.CreateDraft(_authorId, "Tiêu đề gốc", "Nội dung gốc bài viết", null, _now.UtcDateTime);
        _writer.Setup(w => w.GetForUpdateAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(article);

        var useCase = new UpdateDraftArticleUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new UpdateDraftArticleCommand(article.Id, "Tiêu đề mới", "Nội dung bài viết mới cập nhật", null, ExpectedRevision: revision);

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => useCase.ExecuteAsync(command));
        Assert.Equal("Cần cung cấp expectedRevision khác 0.", ex.Message);
    }
}
