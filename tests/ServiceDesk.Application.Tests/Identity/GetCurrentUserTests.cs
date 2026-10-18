using Moq;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Identity.GetCurrentUser;
using ServiceDesk.Application.Identity.Ports;
using ServiceDesk.Application.Identity.Contracts;
using Xunit;

namespace ServiceDesk.Application.Tests.Identity;

public sealed class GetCurrentUserTests
{
    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(true, "invalid")]
    [InlineData(true, "00000000-0000-0000-0000-000000000000")]
    public async Task InvalidActorNeverReachesGateway(bool authenticated, string? id)
    {
        var actor = new Mock<ICurrentUser>();
        actor.SetupGet(u => u.IsAuthenticated).Returns(authenticated);
        actor.SetupGet(u => u.UserId).Returns(id);
        var accounts = new Mock<IUserAccountGateway>(MockBehavior.Strict);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new GetCurrentUserUseCase(accounts.Object, actor.Object).ExecuteAsync());
        accounts.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnlyAuthenticatedSubjectCanBeRead(bool missing)
    {
        var id = Guid.NewGuid();
        var actor = new Mock<ICurrentUser>();
        actor.SetupGet(u => u.IsAuthenticated).Returns(true);
        actor.SetupGet(u => u.UserId).Returns(id.ToString());
        var accounts = new Mock<IUserAccountGateway>(MockBehavior.Strict);
        UserInfo? expected = missing ? null : new() { UserId = id.ToString(), Email = "self@test.invalid" };
        accounts.Setup(a => a.FindByIdAsync(id.ToString("D"), It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        Assert.Same(expected, await new GetCurrentUserUseCase(accounts.Object, actor.Object).ExecuteAsync());
        accounts.VerifyAll();
    }
}
