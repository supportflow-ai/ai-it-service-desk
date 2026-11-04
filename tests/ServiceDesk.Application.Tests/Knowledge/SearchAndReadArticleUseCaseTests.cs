using Moq;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Knowledge.Authorization;
using ServiceDesk.Application.Knowledge.Contracts;
using ServiceDesk.Application.Knowledge.GetArticleDetail;
using ServiceDesk.Application.Knowledge.Ports;
using ServiceDesk.Application.Knowledge.SearchKnowledge;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Knowledge;
using Xunit;

namespace ServiceDesk.Application.Tests.Knowledge;

public sealed class SearchAndReadArticleUseCaseTests
{
    private readonly Mock<IKnowledgeQueries> _queries = new();
    private readonly Mock<ICurrentUser> _user = new();
    private readonly Guid _agentId = Guid.NewGuid();
    private readonly Guid _articleId = Guid.NewGuid();

    private static ArticleDto CreateSampleDto(Guid id, ArticleStatus status, Guid authorId) =>
        new(
            id,
            "Tiêu đề bài viết",
            "Nội dung bài viết chi tiết hợp lệ.",
            status,
            authorId,
            1,
            1u,
            ["tag1"],
            DateTime.UtcNow,
            DateTime.UtcNow,
            status == ArticleStatus.Published ? DateTime.UtcNow : null,
            status == ArticleStatus.Archived ? DateTime.UtcNow : null
        );

    // ==========================================
    // GET ARTICLE DETAIL / NEGATIVE VISIBILITY
    // ==========================================

    [Fact]
    public async Task GetArticleDetail_WhenRequesterQueriesDraftOrArchived_ReturnsNullNegativeVisibility()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        _queries.Setup(q => q.GetByIdAsync(_articleId, It.IsAny<KnowledgeReadScope.Public>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ArticleDto?)null);

        var useCase = new GetArticleDetailUseCase(_queries.Object, _user.Object);
        var result = await useCase.ExecuteAsync(new GetArticleDetailQuery(_articleId));

        Assert.Null(result);
        _queries.Verify(q => q.GetByIdAsync(_articleId, It.IsAny<KnowledgeReadScope.Public>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetArticleDetail_WhenRequesterQueriesPublished_ReturnsArticleDto()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var expectedDto = CreateSampleDto(_articleId, ArticleStatus.Published, Guid.NewGuid());
        _queries.Setup(q => q.GetByIdAsync(_articleId, It.IsAny<KnowledgeReadScope.Public>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var useCase = new GetArticleDetailUseCase(_queries.Object, _user.Object);
        var result = await useCase.ExecuteAsync(new GetArticleDetailQuery(_articleId));

        Assert.NotNull(result);
        Assert.Equal(_articleId, result.Id);
        Assert.Equal(ArticleStatus.Published, result.Status);
    }

    [Fact]
    public async Task GetArticleDetail_WhenAgentReadsOwnDraft_UsesAuthorScope()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _user.SetupGet(u => u.UserId).Returns(_agentId.ToString());

        var expectedDto = CreateSampleDto(_articleId, ArticleStatus.Draft, _agentId);
        _queries.Setup(q => q.GetByIdAsync(_articleId, It.Is<KnowledgeReadScope>(s => s is KnowledgeReadScope.Author && ((KnowledgeReadScope.Author)s).AuthorId == _agentId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var useCase = new GetArticleDetailUseCase(_queries.Object, _user.Object);
        var result = await useCase.ExecuteAsync(new GetArticleDetailQuery(_articleId));

        Assert.NotNull(result);
        Assert.Equal(_articleId, result.Id);
        Assert.Equal(ArticleStatus.Draft, result.Status);
        _queries.Verify(q => q.GetByIdAsync(_articleId, It.Is<KnowledgeReadScope>(s => s is KnowledgeReadScope.Author && ((KnowledgeReadScope.Author)s).AuthorId == _agentId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetArticleDetail_WhenAdminReads_UsesStaffScope()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(true);

        var expectedDto = CreateSampleDto(_articleId, ArticleStatus.Draft, Guid.NewGuid());
        _queries.Setup(q => q.GetByIdAsync(_articleId, It.IsAny<KnowledgeReadScope.Staff>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var useCase = new GetArticleDetailUseCase(_queries.Object, _user.Object);
        var result = await useCase.ExecuteAsync(new GetArticleDetailQuery(_articleId));

        Assert.NotNull(result);
        Assert.Equal(_articleId, result.Id);
        _queries.Verify(q => q.GetByIdAsync(_articleId, It.IsAny<KnowledgeReadScope.Staff>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ==========================================
    // SEARCH KNOWLEDGE
    // ==========================================

    [Fact]
    public async Task SearchKnowledge_WhenRequesterSearches_CallsSearchPublishedAndIgnoresStatusFilter()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var pagedResult = new PagedArticleResult([], 1, 10, 0, 0);
        _queries.Setup(q => q.SearchPublishedAsync("network", "vpn", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var useCase = new SearchKnowledgeUseCase(_queries.Object, _user.Object);
        var query = new SearchKnowledgeQuery(Query: "network", Tag: "vpn", Status: ArticleStatus.Draft, Page: 1, PageSize: 10);

        var result = await useCase.ExecuteAsync(query);

        Assert.NotNull(result);
        _queries.Verify(q => q.SearchPublishedAsync("network", "vpn", 1, 10, It.IsAny<CancellationToken>()), Times.Once);
        _queries.Verify(q => q.SearchStaffAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<ArticleStatus?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchKnowledge_WhenAgentSearches_CallsSearchStaffWithStatusFilter()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var pagedResult = new PagedArticleResult([], 2, 20, 0, 0);
        _queries.Setup(q => q.SearchStaffAsync("printer", "hardware", ArticleStatus.Draft, 2, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var useCase = new SearchKnowledgeUseCase(_queries.Object, _user.Object);
        var query = new SearchKnowledgeQuery(Query: "printer", Tag: "hardware", Status: ArticleStatus.Draft, Page: 2, PageSize: 20);

        var result = await useCase.ExecuteAsync(query);

        Assert.NotNull(result);
        _queries.Verify(q => q.SearchStaffAsync("printer", "hardware", ArticleStatus.Draft, 2, 20, It.IsAny<CancellationToken>()), Times.Once);
        _queries.Verify(q => q.SearchPublishedAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchKnowledge_WhenAdminSearches_CallsSearchStaff()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(true);

        var pagedResult = new PagedArticleResult([], 1, 10, 0, 0);
        _queries.Setup(q => q.SearchStaffAsync(null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var useCase = new SearchKnowledgeUseCase(_queries.Object, _user.Object);
        var query = new SearchKnowledgeQuery();

        var result = await useCase.ExecuteAsync(query);

        Assert.NotNull(result);
        _queries.Verify(q => q.SearchStaffAsync(null, null, null, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchKnowledge_NormalizesPageAndPageSize()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);

        var pagedResult = new PagedArticleResult([], 1, 100, 0, 0);
        _queries.Setup(q => q.SearchStaffAsync(null, null, null, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var useCase = new SearchKnowledgeUseCase(_queries.Object, _user.Object);
        var query = new SearchKnowledgeQuery(Page: -5, PageSize: 500);

        var result = await useCase.ExecuteAsync(query);

        Assert.NotNull(result);
        _queries.Verify(q => q.SearchStaffAsync(null, null, null, 1, 100, It.IsAny<CancellationToken>()), Times.Once);
    }
}
