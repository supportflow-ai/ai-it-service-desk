using Moq;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Application.Reports;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public class ReportUseCaseTests
{
    private readonly Mock<ITicketReportQueries> _reportQueries = new();
    private readonly Mock<ISupportTeamMembershipReader> _membershipReader = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    [Fact]
    public async Task GetSlaPerformanceReport_WhenRequester_ThrowsUnauthorizedAccessException()
    {
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid().ToString());
        _currentUser.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _currentUser.Setup(u => u.IsInRole(It.IsAny<string>())).Returns(false);

        var useCase = new GetSlaPerformanceReportUseCase(_reportQueries.Object, _membershipReader.Object, _currentUser.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            useCase.ExecuteAsync(new(null, null, null), CancellationToken.None));
    }

    [Fact]
    public async Task GetSlaPerformanceReport_WhenAgent_ReturnsReportData()
    {
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid().ToString());
        _currentUser.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);

        var expectedReport = new SlaPerformanceReportDto(
            TotalTickets: 10,
            ResolvedTickets: 8,
            FirstResponseBreachedCount: 2,
            FirstResponseComplianceRate: 80.0,
            ResolutionBreachedCount: 1,
            ResolutionComplianceRate: 90.0,
            AverageMttaMinutes: 15.5,
            AverageMttrMinutes: 120.0);

        _reportQueries.Setup(q => q.GetSlaPerformanceAsync(
                It.IsAny<TicketReadScope>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        var useCase = new GetSlaPerformanceReportUseCase(_reportQueries.Object, _membershipReader.Object, _currentUser.Object);

        var result = await useCase.ExecuteAsync(new(null, null, null), CancellationToken.None);

        Assert.Equal(10, result.TotalTickets);
        Assert.Equal(80.0, result.FirstResponseComplianceRate);
        Assert.Equal(90.0, result.ResolutionComplianceRate);
        Assert.Equal(15.5, result.AverageMttaMinutes);
    }

    [Fact]
    public async Task GetTicketDistributionReport_WhenRequester_ThrowsUnauthorizedAccessException()
    {
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid().ToString());
        _currentUser.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _currentUser.Setup(u => u.IsInRole(It.IsAny<string>())).Returns(false);

        var useCase = new GetTicketDistributionReportUseCase(_reportQueries.Object, _membershipReader.Object, _currentUser.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            useCase.ExecuteAsync(new(null, null, null), CancellationToken.None));
    }

    [Fact]
    public async Task GetTicketDistributionReport_WhenAdmin_ReturnsReportData()
    {
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid().ToString());
        _currentUser.SetupGet(u => u.Roles).Returns([RoleNames.Admin]);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(true);

        var expectedReport = new TicketDistributionReportDto(
            ByStatus: new Dictionary<string, int> { ["Open"] = 2, ["InProgress"] = 3 },
            ByPriority: new Dictionary<string, int> { ["High"] = 5 },
            ByCategory: new Dictionary<string, int> { ["Hardware"] = 5 },
            ByAssignee: [new AgentWorkloadDto(Guid.NewGuid(), "Agent Test", 3, 2)]);

        _reportQueries.Setup(q => q.GetDistributionAsync(
                It.IsAny<TicketReadScope>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        var useCase = new GetTicketDistributionReportUseCase(_reportQueries.Object, _membershipReader.Object, _currentUser.Object);

        var result = await useCase.ExecuteAsync(new(null, null, null), CancellationToken.None);

        Assert.Equal(2, result.ByStatus["Open"]);
        Assert.Single(result.ByAssignee);
    }
}
