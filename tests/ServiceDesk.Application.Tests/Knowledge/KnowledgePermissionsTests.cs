using Moq;
using ServiceDesk.Modules.Knowledge.Application.Authorization;
using ServiceDesk.Modules.Knowledge.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.Application.Tests.Knowledge;

/// <summary>Ma trận quyền của Knowledge.</summary>
public sealed class KnowledgePermissionsTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc);

    private static ICurrentUser UserInRoles(Guid? id, params string[] roles)
    {
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.IsInRole(It.IsAny<string>())).Returns<string>(role => roles.Contains(role));
        user.SetupGet(u => u.IsAuthenticated).Returns(id.HasValue);
        user.SetupGet(u => u.UserId).Returns(id?.ToString());
        return user.Object;
    }

    [Theory]
    [InlineData(RoleNames.Agent, true)]
    [InlineData(RoleNames.Admin, true)]
    [InlineData(RoleNames.Requester, false)]
    public void OnlyAgentOrAdminIsStaff(string role, bool expected) =>
        Assert.Equal(expected, KnowledgePermissions.IsStaff(UserInRoles(Guid.NewGuid(), role)));

    [Fact]
    public void AuthorOrAdminCanManageAnArticle()
    {
        var author = Guid.NewGuid();
        var article = Article.CreateDraft(author, "Valid Title", "Valid Content at least 10 chars", null, Now);

        Assert.True(KnowledgePermissions.CanManage(article, author, UserInRoles(author, RoleNames.Agent)));
        Assert.True(KnowledgePermissions.CanManage(article, Guid.NewGuid(), UserInRoles(Guid.NewGuid(), RoleNames.Admin)));
        Assert.False(KnowledgePermissions.CanManage(article, Guid.NewGuid(), UserInRoles(Guid.NewGuid(), RoleNames.Agent)));
    }

    [Fact]
    public void ReadScopeDependsOnRole()
    {
        var agent = Guid.NewGuid();

        Assert.IsType<KnowledgeReadScope.Staff>(KnowledgePermissions.ReadScope(UserInRoles(Guid.NewGuid(), RoleNames.Admin)));
        Assert.Equal(new KnowledgeReadScope.Author(agent), KnowledgePermissions.ReadScope(UserInRoles(agent, RoleNames.Agent)));
        Assert.IsType<KnowledgeReadScope.Public>(KnowledgePermissions.ReadScope(UserInRoles(Guid.NewGuid(), RoleNames.Requester)));
        Assert.IsType<KnowledgeReadScope.Public>(KnowledgePermissions.ReadScope(UserInRoles(null)));
    }
}
