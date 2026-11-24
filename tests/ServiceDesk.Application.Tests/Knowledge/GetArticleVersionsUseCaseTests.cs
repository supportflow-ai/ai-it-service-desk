using Moq;
using ServiceDesk.Modules.Knowledge.Application.Authorization;
using ServiceDesk.Modules.Knowledge.Application.Contracts;
using ServiceDesk.Modules.Knowledge.Application.GetArticleVersions;
using ServiceDesk.Modules.Knowledge.Application.Ports;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.Application.Tests.Knowledge;

public sealed class GetArticleVersionsUseCaseTests
{
    private readonly Mock<IKnowledgeQueries> _queries = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    [Fact]
    public async Task ExecuteAsync_ReturnsVersionsFromQuery()
    {
        var articleId = Guid.NewGuid();
        var versions = new List<ArticleVersionDto>
        {
            new(Guid.NewGuid(), articleId, 2, "Title V2", "Content V2", Guid.NewGuid(), DateTime.UtcNow),
            new(Guid.NewGuid(), articleId, 1, "Title V1", "Content V1", Guid.NewGuid(), DateTime.UtcNow.AddHours(-1))
        };

        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid().ToString());
        _currentUser.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);

        _queries.Setup(q => q.GetVersionsAsync(articleId, It.IsAny<KnowledgeReadScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(versions);

        var useCase = new GetArticleVersionsUseCase(_queries.Object, _currentUser.Object);
        var result = await useCase.ExecuteAsync(new GetArticleVersionsQuery(articleId));

        Assert.Equal(2, result.Count);
        Assert.Equal(2, result[0].VersionNumber);
    }
}
