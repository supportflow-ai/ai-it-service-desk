using Moq;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.GetUsersByRole;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.Application.Tests.Identity;

public sealed class GetUsersByRoleUseCaseTests
{
    private readonly Mock<IUserAccountGateway> _userGateway = new();
    private readonly Mock<ICurrentUser> _user = new();

    public GetUsersByRoleUseCaseTests()
    {
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);
        _user.SetupGet(u => u.UserId).Returns(Guid.NewGuid().ToString());
    }

    [Fact]
    public async Task Execute_AsAgent_ReturnsAgentList()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);

        var agents = new List<UserInfo>
        {
            new() { UserId = Guid.NewGuid().ToString(), Email = "agent1@test.com", FullName = "Agent One", Roles = [RoleNames.Agent] },
            new() { UserId = Guid.NewGuid().ToString(), Email = "agent2@test.com", FullName = "Agent Two", Roles = [RoleNames.Agent] }
        };

        _userGateway.Setup(g => g.GetUsersInRoleAsync(RoleNames.Agent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        var useCase = new GetUsersByRoleUseCase(_userGateway.Object, _user.Object);
        var result = await useCase.ExecuteAsync(new(RoleNames.Agent));

        Assert.Equal(2, result.Count);
        Assert.All(result, user => Assert.False(string.IsNullOrWhiteSpace(user.FullName)));
        _userGateway.VerifyAll();
    }

    [Fact]
    public async Task Execute_RejectsRequesterRole()
    {
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        var useCase = new GetUsersByRoleUseCase(_userGateway.Object, _user.Object);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(new(RoleNames.Requester)));
        _userGateway.Verify(g => g.GetUsersInRoleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_AsRequester_ThrowsUnauthorized()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var useCase = new GetUsersByRoleUseCase(_userGateway.Object, _user.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            useCase.ExecuteAsync(new(RoleNames.Agent)));
    }
}
