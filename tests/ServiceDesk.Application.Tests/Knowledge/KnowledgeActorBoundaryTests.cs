using Microsoft.Extensions.DependencyInjection;
using Moq;
using ServiceDesk.Modules.Knowledge.Application.ArchiveArticle;
using ServiceDesk.Modules.Knowledge.Application.CreateDraftArticle;
using ServiceDesk.Modules.Knowledge.Application.GetArticleDetail;
using ServiceDesk.Modules.Knowledge.Application.GetArticleVersions;
using ServiceDesk.Modules.Knowledge.Application.GetKnowledgeTags;
using ServiceDesk.Modules.Knowledge.Application.Ports;
using ServiceDesk.Modules.Knowledge.Application.PublishArticle;
using ServiceDesk.Modules.Knowledge.Application.RepublishArticle;
using ServiceDesk.Modules.Knowledge.Application.SearchKnowledge;
using ServiceDesk.Modules.Knowledge.Application.UpdateDraftArticle;
using ServiceDesk.Modules.Knowledge.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;
using Xunit;

namespace ServiceDesk.Application.Tests.Knowledge;

public sealed class KnowledgeActorBoundaryTests
{
    public static IEnumerable<object?[]> InvalidActors()
    {
        foreach (var operation in new[] { "detail", "versions", "tags", "search", "create", "update", "publish", "republish", "archive" })
        {
            yield return [operation, false, Guid.NewGuid().ToString()];
            foreach (var id in new[] { null, "", "invalid", Guid.Empty.ToString() }) yield return [operation, true, id];
        }
    }

    [Theory]
    [MemberData(nameof(InvalidActors))]
    public async Task RejectsInvalidActorBeforeReadingOrWriting(string operation, bool authenticated, string? id)
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.IsAuthenticated).Returns(authenticated);
        user.SetupGet(u => u.UserId).Returns(id);
        user.Setup(u => u.IsInRole(It.IsAny<string>())).Returns(true);
        var queries = new Mock<IKnowledgeQueries>();
        var writer = new Mock<IKnowledgeWriter>();
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        var article = Article.CreateDraft(Guid.NewGuid(), "Valid article", "Valid article content", null, DateTime.UtcNow);
        writer.Setup(w => w.GetForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(article);
        using var provider = new ServiceCollection().AddSingleton(user.Object).BuildServiceProvider();
        Func<Task> run = operation switch
        {
            "detail" => () => new GetArticleDetailUseCase(queries.Object, user.Object).ExecuteAsync(new(Guid.NewGuid())),
            "versions" => () => new GetArticleVersionsUseCase(queries.Object, user.Object).ExecuteAsync(new(Guid.NewGuid())),
            "tags" => () => ActivatorUtilities.CreateInstance<GetKnowledgeTagsUseCase>(provider, queries.Object).ExecuteAsync(new()),
            "search" => () => new SearchKnowledgeUseCase(queries.Object, user.Object).ExecuteAsync(new()),
            "create" => () => new CreateDraftArticleUseCase(writer.Object, user.Object, clock.Object).ExecuteAsync(new("Title", "Valid content", null)),
            "update" => () => new UpdateDraftArticleUseCase(writer.Object, user.Object, clock.Object).ExecuteAsync(new(article.Id, "New title", "New content", null, 1)),
            "publish" => () => new PublishArticleUseCase(writer.Object, user.Object, clock.Object).ExecuteAsync(new(article.Id, 1)),
            "republish" => () => new RepublishArticleUseCase(writer.Object, user.Object, clock.Object).ExecuteAsync(new(article.Id, "New title", "New content", null, 1)),
            _ => () => new ArchiveArticleUseCase(writer.Object, user.Object, clock.Object).ExecuteAsync(new(article.Id, 1))
        };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(run);
        queries.VerifyNoOtherCalls();
        writer.VerifyNoOtherCalls();
    }
}
