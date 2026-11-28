using Moq;
using ServiceDesk.Modules.Knowledge.Application.Contracts;
using ServiceDesk.Modules.Knowledge.Application.GetKnowledgeTags;
using ServiceDesk.Modules.Knowledge.Application.Ports;
using Xunit;

namespace ServiceDesk.Application.Tests.Knowledge;

public sealed class GetKnowledgeTagsUseCaseTests
{
    private readonly Mock<IKnowledgeQueries> _queries = new();

    [Fact]
    public async Task ExecuteAsync_ReturnsTagsFromQuery()
    {
        var tags = new List<TagSummaryDto>
        {
            new("vpn", 5),
            new("email", 3)
        };

        _queries.Setup(q => q.GetTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tags);

        var user = new Mock<ServiceDesk.SharedKernel.Application.Abstractions.ICurrentUser>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.UserId).Returns(Guid.NewGuid().ToString());
        var useCase = new GetKnowledgeTagsUseCase(_queries.Object, user.Object);
        var result = await useCase.ExecuteAsync(new GetKnowledgeTagsQuery());

        Assert.Equal(2, result.Count);
        Assert.Equal("vpn", result[0].Name);
        Assert.Equal(5, result[0].ArticleCount);
    }
}
