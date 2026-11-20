using Moq;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.GetAgentQueue;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public sealed class GetAgentQueueUseCaseTests
{
    private readonly Mock<ITicketQueries> _queries = new(MockBehavior.Strict);
    private readonly Mock<ICurrentUser> _user = new();

    public GetAgentQueueUseCaseTests()
    {
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);
        _user.SetupGet(u => u.UserId).Returns(Guid.NewGuid().ToString());
    }

    [Fact]
    public async Task Execute_AsAgent_ReturnsQueueResults()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var pagedResult = new PagedTicketResult([], 1, 10, 0);
        var actorId = Guid.Parse(_user.Object.UserId!);
        _queries.Setup(q => q.GetAgentQueueAsync(actorId, new TicketReadScope.AgentTickets(actorId, false), null, null, "unassigned", null, false, 1, 10, null, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var useCase = new GetAgentQueueUseCase(_queries.Object, _user.Object);
        var result = await useCase.ExecuteAsync(new(Scope: "unassigned"));

        Assert.NotNull(result);
        _queries.VerifyAll();
    }

    [Fact]
    public async Task Execute_WithPriority_PassesFilterAndSortToQuery()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        var result = new PagedTicketResult([], 1, 10, 0);
        var actorId = Guid.Parse(_user.Object.UserId!);
        _queries.Setup(q => q.GetAgentQueueAsync(actorId, new TicketReadScope.AgentTickets(actorId, false), null, null, null, "P1", true, 1, 10, null, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var useCase = new GetAgentQueueUseCase(_queries.Object, _user.Object);
        await useCase.ExecuteAsync(new(Priority: "P1", SortByPriority: true));

        _queries.VerifyAll();
    }

    [Theory]
    [InlineData("All")]
    [InlineData("OnTrack")]
    [InlineData("AtRisk")]
    [InlineData("Breached")]
    [InlineData("breached")]
    public async Task Execute_WithValidSlaStatus_PassesFilterToQuery(string slaStatus)
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        var result = new PagedTicketResult([], 1, 10, 0);
        var actorId = Guid.Parse(_user.Object.UserId!);
        _queries.Setup(q => q.GetAgentQueueAsync(actorId, new TicketReadScope.AgentTickets(actorId, false), null, null, null, null, false, 1, 10, slaStatus, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var useCase = new GetAgentQueueUseCase(_queries.Object, _user.Object);
        await useCase.ExecuteAsync(new(SlaStatus: slaStatus));

        _queries.VerifyAll();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("Completed")]
    [InlineData("NotApplicable")]
    [InlineData("123")]
    public async Task Execute_WithInvalidSlaStatus_ThrowsArgumentException(string invalidSlaStatus)
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var useCase = new GetAgentQueueUseCase(_queries.Object, _user.Object);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(new(SlaStatus: invalidSlaStatus)));

        Assert.Equal("slaStatus must be one of All, OnTrack, AtRisk, Breached", ex.Message);
    }

    [Fact]
    public async Task Execute_AsRequester_ThrowsUnauthorizedAccessException()
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var useCase = new GetAgentQueueUseCase(_queries.Object, _user.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            useCase.ExecuteAsync(new()));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Execute_InvalidPaging_ThrowsArgumentException(int page, int pageSize)
    {
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _user.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _user.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var useCase = new GetAgentQueueUseCase(_queries.Object, _user.Object);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(new(Page: page, PageSize: pageSize)));
    }
}
