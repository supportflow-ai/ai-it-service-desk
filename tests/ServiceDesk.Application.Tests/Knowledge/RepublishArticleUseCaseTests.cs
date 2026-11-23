using Moq;
using ServiceDesk.Modules.Knowledge.Application.Contracts;
using ServiceDesk.Modules.Knowledge.Application.Ports;
using ServiceDesk.Modules.Knowledge.Application.RepublishArticle;
using ServiceDesk.Modules.Knowledge.Domain;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.Application.Tests.Knowledge;

public sealed class RepublishArticleUseCaseTests
{
    private readonly Mock<IKnowledgeWriter> _writer = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IClock> _clock = new();

    private readonly Guid _authorId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc);

    public RepublishArticleUseCaseTests()
    {
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(_authorId.ToString());
        _currentUser.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _clock.SetupGet(c => c.UtcNow).Returns(new DateTimeOffset(_now));
    }

    [Fact]
    public async Task ExecuteAsync_AsAuthor_SucceedsAndIncrementsVersion()
    {
        var article = Article.CreateDraft(_authorId, "Initial Title", "Initial content at least ten chars", ["vpn"], _now);
        article.Publish(_authorId, _now);

        _writer.Setup(w => w.GetForUpdateAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);
        _writer.Setup(w => w.GetRevision(article)).Returns(1u);
        _writer.Setup(w => w.UpdateArticleAsync(article, 1u, It.IsAny<CancellationToken>())).ReturnsAsync(article);

        var useCase = new RepublishArticleUseCase(_writer.Object, _currentUser.Object, _clock.Object);
        var command = new RepublishArticleCommand(article.Id, "Updated Title", "Updated content at least ten chars", ["vpn", "v2"], 1u);

        var result = await useCase.ExecuteAsync(command);

        Assert.Equal("Updated Title", result.Title);
        Assert.Equal(2, result.Version);
        Assert.Equal(ArticleStatus.Published, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_MismatchedRevision_ThrowsConcurrencyConflictException()
    {
        var article = Article.CreateDraft(_authorId, "Initial Title", "Initial content at least ten chars", ["vpn"], _now);
        article.Publish(_authorId, _now);

        _writer.Setup(w => w.GetForUpdateAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);
        _writer.Setup(w => w.GetRevision(article)).Returns(2u);

        var useCase = new RepublishArticleUseCase(_writer.Object, _currentUser.Object, _clock.Object);
        var command = new RepublishArticleCommand(article.Id, "Updated Title", "Updated content at least ten chars", ["vpn"], 1u);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task ExecuteAsync_NonStaff_ThrowsUnauthorizedAccessException()
    {
        _currentUser.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var useCase = new RepublishArticleUseCase(_writer.Object, _currentUser.Object, _clock.Object);
        var command = new RepublishArticleCommand(Guid.NewGuid(), "Updated Title", "Updated content at least ten chars", null, 1u);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => useCase.ExecuteAsync(command));
    }
}
