using Moq;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Application.SupportTeams;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public class SupportTeamUseCaseTests
{
    private readonly Mock<ISupportTeamStore> _store = new();
    private readonly Mock<ISupportTeamMembershipReader> _membershipReader = new();
    private readonly Mock<IUserDirectory> _userDirectory = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _agentId = Guid.NewGuid();
    private readonly Guid _leadId = Guid.NewGuid();
    private readonly Guid _teamId = Guid.NewGuid();

    public SupportTeamUseCaseTests()
    {
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(_adminId.ToString());
        _currentUser.SetupGet(u => u.Roles).Returns([RoleNames.Admin]);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(true);
    }

    [Fact]
    public async Task GetSupportTeams_AsStaff_ReturnsTeamsWithMembers()
    {
        var team = SupportTeam.ManualTriageId;
        _store.Setup(s => s.GetActiveTeamsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([ServiceDesk.Modules.Ticketing.Domain.SupportTeam.Create("IT_SUPPORT", "IT Support")]);
        _store.Setup(s => s.GetMembersAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([SupportTeamMember.Create(_teamId, _agentId, isLead: true)]);
        _userDirectory.Setup(d => d.FindDisplayNameAsync(_agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Agent Demo");

        var useCase = new GetSupportTeamsUseCase(_store.Object, _userDirectory.Object, _currentUser.Object);
        var result = await useCase.ExecuteAsync();

        Assert.Single(result);
        Assert.Single(result[0].Members);
        Assert.Equal("Agent Demo", result[0].Members[0].DisplayName);
        Assert.True(result[0].Members[0].IsLead);
    }

    [Fact]
    public async Task GetSupportTeams_AsRequester_ThrowsUnauthorized()
    {
        _currentUser.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Requester)).Returns(true);

        var useCase = new GetSupportTeamsUseCase(_store.Object, _userDirectory.Object, _currentUser.Object);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => useCase.ExecuteAsync());
    }

    [Fact]
    public async Task AddTeamMember_AsAdmin_AddsSuccessfully()
    {
        _store.Setup(s => s.GetTeamByIdAsync(_teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceDesk.Modules.Ticketing.Domain.SupportTeam.Create("IT_SUPPORT", "IT Support"));
        _userDirectory.Setup(d => d.IsInAnyRoleAsync(_agentId, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _store.Setup(s => s.ExistsMemberAsync(_teamId, _agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userDirectory.Setup(d => d.FindDisplayNameAsync(_agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Agent Demo");

        var useCase = new AddTeamMemberUseCase(_store.Object, _membershipReader.Object, _userDirectory.Object, _currentUser.Object);
        var result = await useCase.ExecuteAsync(new(_teamId, new(_agentId, IsLead: true)));

        Assert.Equal(_agentId, result.UserId);
        Assert.True(result.IsLead);
        _store.Verify(s => s.AddMemberAsync(It.Is<SupportTeamMember>(m => m.TeamId == _teamId && m.UserId == _agentId && m.IsLead), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddTeamMember_WhenAlreadyMember_ThrowsConflict()
    {
        _store.Setup(s => s.GetTeamByIdAsync(_teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceDesk.Modules.Ticketing.Domain.SupportTeam.Create("IT_SUPPORT", "IT Support"));
        _userDirectory.Setup(d => d.IsInAnyRoleAsync(_agentId, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _store.Setup(s => s.ExistsMemberAsync(_teamId, _agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var useCase = new AddTeamMemberUseCase(_store.Object, _membershipReader.Object, _userDirectory.Object, _currentUser.Object);
        await Assert.ThrowsAsync<TeamMemberConflictException>(() => useCase.ExecuteAsync(new(_teamId, new(_agentId))));
    }

    [Fact]
    public async Task AddTeamMember_WhenNotStaff_ThrowsArgumentException()
    {
        _store.Setup(s => s.GetTeamByIdAsync(_teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceDesk.Modules.Ticketing.Domain.SupportTeam.Create("IT_SUPPORT", "IT Support"));
        _userDirectory.Setup(d => d.IsInAnyRoleAsync(_agentId, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var useCase = new AddTeamMemberUseCase(_store.Object, _membershipReader.Object, _userDirectory.Object, _currentUser.Object);
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(new(_teamId, new(_agentId))));
    }

    [Fact]
    public async Task RemoveTeamMember_AsAdmin_RemovesSuccessfully()
    {
        _store.Setup(s => s.GetTeamByIdAsync(_teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceDesk.Modules.Ticketing.Domain.SupportTeam.Create("IT_SUPPORT", "IT Support"));
        _store.Setup(s => s.ExistsMemberAsync(_teamId, _agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var useCase = new RemoveTeamMemberUseCase(_store.Object, _membershipReader.Object, _currentUser.Object);
        await useCase.ExecuteAsync(new(_teamId, _agentId));

        _store.Verify(s => s.RemoveMemberAsync(_teamId, _agentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveTeamMember_WhenMemberNotFound_ThrowsNotFound()
    {
        _store.Setup(s => s.GetTeamByIdAsync(_teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceDesk.Modules.Ticketing.Domain.SupportTeam.Create("IT_SUPPORT", "IT Support"));
        _store.Setup(s => s.ExistsMemberAsync(_teamId, _agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var useCase = new RemoveTeamMemberUseCase(_store.Object, _membershipReader.Object, _currentUser.Object);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => useCase.ExecuteAsync(new(_teamId, _agentId)));
    }

    [Fact]
    public async Task CreateSupportTeam_WhenNotAdmin_ThrowsUnauthorized()
    {
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        var useCase = new CreateSupportTeamUseCase(_store.Object, _currentUser.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            useCase.ExecuteAsync(new("NET_DEV", "Network Development")));
    }

    [Fact]
    public async Task CreateSupportTeam_WhenCodeExists_ThrowsConflict()
    {
        _store.Setup(s => s.ExistsCodeAsync("NET_DEV", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var useCase = new CreateSupportTeamUseCase(_store.Object, _currentUser.Object);

        await Assert.ThrowsAsync<SupportTeamConflictException>(() =>
            useCase.ExecuteAsync(new("NET_DEV", "Network Development")));
    }

    [Fact]
    public async Task CreateSupportTeam_WhenValid_ReturnsCreatedDto()
    {
        _store.Setup(s => s.ExistsCodeAsync("NET_DEV", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var useCase = new CreateSupportTeamUseCase(_store.Object, _currentUser.Object);

        var result = await useCase.ExecuteAsync(new("net_dev", "Network Development"));

        Assert.Equal("NET_DEV", result.Code);
        Assert.Equal("Network Development", result.Name);
        Assert.True(result.IsActive);
        _store.Verify(s => s.AddTeamAsync(It.Is<SupportTeam>(t => t.Code == "NET_DEV"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateSupportTeam_WhenValid_UpdatesAndReturnsDto()
    {
        var team = SupportTeam.Create("NET_DEV", "Old Name");
        _store.Setup(s => s.GetTeamByIdAsync(team.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);
        _store.Setup(s => s.GetMembersAsync(team.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var agentDirectory = new Mock<IAgentDirectory>();
        var useCase = new UpdateSupportTeamUseCase(_store.Object, agentDirectory.Object, _currentUser.Object);

        var result = await useCase.ExecuteAsync(team.Id, new("New Name", false));

        Assert.Equal("New Name", result.Name);
        Assert.False(result.IsActive);
        _store.Verify(s => s.UpdateTeamAsync(It.Is<SupportTeam>(t => t.Name == "New Name" && !t.IsActive), It.IsAny<CancellationToken>()), Times.Once);
    }
}
