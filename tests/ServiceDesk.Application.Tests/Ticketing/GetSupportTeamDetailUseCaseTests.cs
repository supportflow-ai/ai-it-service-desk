using Moq;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Application.SupportTeams;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public sealed class GetSupportTeamDetailUseCaseTests
{
    private readonly Mock<ISupportTeamStore> _store = new();
    private readonly Mock<IUserDirectory> _userDirectory = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    private readonly Guid _agentId = Guid.NewGuid();
    private readonly Guid _teamId = Guid.NewGuid();

    public GetSupportTeamDetailUseCaseTests()
    {
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(_agentId.ToString());
        _currentUser.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
    }

    [Fact]
    public async Task ExecuteAsync_TeamExists_ReturnsDetailWithMembers()
    {
        var team = ServiceDesk.Modules.Ticketing.Domain.SupportTeam.Create("IT_SUPPORT", "IT Support Team");
        var memberId = Guid.NewGuid();

        _store.Setup(s => s.GetTeamByIdAsync(team.Id, It.IsAny<CancellationToken>())).ReturnsAsync(team);
        _store.Setup(s => s.GetMembersAsync(team.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([SupportTeamMember.Create(team.Id, memberId, isLead: true)]);
        _userDirectory.Setup(d => d.FindDisplayNameAsync(memberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Lead Tech");

        var useCase = new GetSupportTeamDetailUseCase(_store.Object, _userDirectory.Object, _currentUser.Object);
        var result = await useCase.ExecuteAsync(new GetSupportTeamDetailQuery(team.Id));

        Assert.NotNull(result);
        Assert.Equal("IT_SUPPORT", result.Code);
        Assert.Single(result.Members);
        Assert.Equal("Lead Tech", result.Members[0].DisplayName);
        Assert.True(result.Members[0].IsLead);
    }

    [Fact]
    public async Task ExecuteAsync_TeamNotFound_ReturnsNull()
    {
        _store.Setup(s => s.GetTeamByIdAsync(_teamId, It.IsAny<CancellationToken>())).ReturnsAsync((SupportTeam?)null);

        var useCase = new GetSupportTeamDetailUseCase(_store.Object, _userDirectory.Object, _currentUser.Object);
        var result = await useCase.ExecuteAsync(new GetSupportTeamDetailQuery(_teamId));

        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteAsync_NonStaff_ThrowsUnauthorizedAccessException()
    {
        _currentUser.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var useCase = new GetSupportTeamDetailUseCase(_store.Object, _userDirectory.Object, _currentUser.Object);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => useCase.ExecuteAsync(new GetSupportTeamDetailQuery(_teamId)));
    }
}
