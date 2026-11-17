using FluentAssertions;
using Moq;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.GetTicketAnalytics;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public sealed class GetTicketAnalyticsUseCaseTests
{
    private readonly Mock<ITicketAnalyticsQuery> _query = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Guid _agentId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);

    public GetTicketAnalyticsUseCaseTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(new DateTimeOffset(_now));
        _currentUser.SetupGet(u => u.UserId).Returns(_agentId.ToString());
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.Roles).Returns([RoleNames.Agent]);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Requester)).Returns(false);
    }

    private GetTicketAnalyticsUseCase CreateUseCase() =>
        new(_query.Object, _currentUser.Object, _clock.Object);

    [Fact]
    public async Task ExecuteAsync_WhenQueryIsNull_ThrowsArgumentNullException()
    {
        var useCase = CreateUseCase();
        var act = () => useCase.ExecuteAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null, "2026-11-15T00:00:00Z")]
    [InlineData("2026-11-01T00:00:00Z", null)]
    [InlineData("", "2026-11-15T00:00:00Z")]
    [InlineData("2026-11-01T00:00:00Z", "   ")]
    public async Task ExecuteAsync_WhenFromOrToMissing_ThrowsArgumentException(string? from, string? to)
    {
        var useCase = CreateUseCase();
        var act = () => useCase.ExecuteAsync(new(from, to));
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*both from and to are required*");
    }

    [Theory]
    [InlineData("2026-11-01", "2026-11-15T00:00:00Z")] // Ngày thuần không có giờ và timezone
    [InlineData("2026-11-01T00:00:00Z", "2026-11-15")]
    [InlineData("2026-11-01T12:00:00", "2026-11-15T12:00:00Z")] // Thiếu timezone offset/Z
    [InlineData("invalid-date", "2026-11-15T12:00:00Z")]
    public async Task ExecuteAsync_WhenInvalidDateFormatOrLacksTimezone_ThrowsArgumentException(string from, string to)
    {
        var useCase = CreateUseCase();
        var act = () => useCase.ExecuteAsync(new(from, to));
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*ISO-8601 timestamp with explicit timezone*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenFromGreaterThanOrEqualToTo_ThrowsArgumentException()
    {
        var useCase = CreateUseCase();
        var act = () => useCase.ExecuteAsync(new("2026-11-15T00:00:00Z", "2026-11-01T00:00:00Z"));
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*from must be earlier than to*");

        var actEqual = () => useCase.ExecuteAsync(new("2026-11-15T00:00:00Z", "2026-11-15T00:00:00Z"));
        await actEqual.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*from must be earlier than to*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenRangeExceeds90Days_ThrowsArgumentException()
    {
        var useCase = CreateUseCase();
        var from = "2026-01-01T00:00:00Z";
        var to = "2026-04-01T00:00:01Z"; // 90 days + 1 second
        var act = () => useCase.ExecuteAsync(new(from, to));
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*date range cannot exceed 90 days*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenRangeExactly90Days_IsValid()
    {
        var useCase = CreateUseCase();
        var fromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = fromUtc.AddDays(90);

        _query.Setup(q => q.QueryAnalyticsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmptyResult());

        var result = await useCase.ExecuteAsync(new(fromUtc.ToString("O"), toUtc.ToString("O")));

        result.Should().NotBeNull();
        result.Range.From.Should().Be(fromUtc);
        result.Range.To.Should().Be(toUtc);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidOffsetTimezone_NormalizesToUtc()
    {
        var useCase = CreateUseCase();
        // +07:00: 2026-11-01T07:00:00+07:00 -> 2026-11-01T00:00:00Z
        var fromStr = "2026-11-01T07:00:00+07:00";
        var toStr = "2026-11-15T07:00:00+07:00";

        var expectedFromUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var expectedToUtc = new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc);

        _query.Setup(q => q.QueryAnalyticsAsync(expectedFromUtc, expectedToUtc, _now, false, _agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmptyResult());

        var result = await useCase.ExecuteAsync(new(fromStr, toStr));

        result.Range.From.Should().Be(expectedFromUtc);
        result.Range.To.Should().Be(expectedToUtc);
        result.EvaluatedAt.Should().Be(_now);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFutureRange_IsValid_DoesNotClamp()
    {
        var useCase = CreateUseCase();
        var futureFrom = _now.AddDays(10);
        var futureTo = _now.AddDays(20);

        _query.Setup(q => q.QueryAnalyticsAsync(futureFrom, futureTo, _now, false, _agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmptyResult());

        var result = await useCase.ExecuteAsync(new(futureFrom.ToString("O"), futureTo.ToString("O")));

        result.Range.From.Should().Be(futureFrom);
        result.Range.To.Should().Be(futureTo);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequesterOnly_ThrowsUnauthorizedAccessException()
    {
        _currentUser.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Requester)).Returns(true);

        var useCase = CreateUseCase();
        var act = () => useCase.ExecuteAsync(new("2026-11-01T00:00:00Z", "2026-11-15T00:00:00Z"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Only agents or admins can access analytics.*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenAdmin_CallsQueryWithIsAdminTrue()
    {
        _currentUser.SetupGet(u => u.Roles).Returns([RoleNames.Admin]);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(true);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(false);

        var useCase = CreateUseCase();
        _query.Setup(q => q.QueryAnalyticsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), true, _agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmptyResult());

        await useCase.ExecuteAsync(new("2026-11-01T00:00:00Z", "2026-11-15T00:00:00Z"));

        _query.Verify(q => q.QueryAnalyticsAsync(
            new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc),
            _now,
            true,
            _agentId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAdminAndAgent_PrefersAdmin()
    {
        _currentUser.SetupGet(u => u.Roles).Returns([RoleNames.Admin, RoleNames.Agent]);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(true);
        _currentUser.Setup(u => u.IsInRole(RoleNames.Agent)).Returns(true);

        var useCase = CreateUseCase();
        _query.Setup(q => q.QueryAnalyticsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), true, _agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmptyResult());

        await useCase.ExecuteAsync(new("2026-11-01T00:00:00Z", "2026-11-15T00:00:00Z"));

        _query.Verify(q => q.QueryAnalyticsAsync(
            It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            true, // isAdmin = true
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CalculatesComplianceRateAndAverageResolution_Correctly()
    {
        var useCase = CreateUseCase();
        var queryResult = new TicketAnalyticsQueryResult(
            Volume: 10,
            StatusDistribution: new Dictionary<string, int> { ["Resolved"] = 6, ["InProgress"] = 4 },
            PriorityDistribution: new Dictionary<string, int> { ["P1"] = 2, ["P2"] = 3, ["P3"] = 5 },
            FirstResponseAchieved: 4,
            FirstResponseBreached: 2, // 4 / (4 + 2) = 66.67%
            ResolutionAchieved: 28,
            ResolutionBreached: 4,    // 28 / (28 + 4) = 87.5%
            ResolutionEligibleCount: 3,
            ResolutionTotalMinutes: 540.0 // 540 / 3 = 180.0
        );

        _query.Setup(q => q.QueryAnalyticsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);

        var result = await useCase.ExecuteAsync(new("2026-11-01T00:00:00Z", "2026-11-15T00:00:00Z"));

        result.Volume.Should().Be(10);
        result.FirstResponseCompliance.Achieved.Should().Be(4);
        result.FirstResponseCompliance.Breached.Should().Be(2);
        result.FirstResponseCompliance.Rate.Should().Be(66.67);

        result.ResolutionCompliance.Achieved.Should().Be(28);
        result.ResolutionCompliance.Breached.Should().Be(4);
        result.ResolutionCompliance.Rate.Should().Be(87.5);

        result.ResolutionEligibleCount.Should().Be(3);
        result.AverageResolutionMinutes.Should().Be(180.0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenZeroDenominator_ReturnsNullRates()
    {
        var useCase = CreateUseCase();
        var queryResult = new TicketAnalyticsQueryResult(
            Volume: 5,
            StatusDistribution: new Dictionary<string, int>(),
            PriorityDistribution: new Dictionary<string, int>(),
            FirstResponseAchieved: 0,
            FirstResponseBreached: 0,
            ResolutionAchieved: 0,
            ResolutionBreached: 0,
            ResolutionEligibleCount: 0,
            ResolutionTotalMinutes: 0.0
        );

        _query.Setup(q => q.QueryAnalyticsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);

        var result = await useCase.ExecuteAsync(new("2026-11-01T00:00:00Z", "2026-11-15T00:00:00Z"));

        result.FirstResponseCompliance.Achieved.Should().Be(0);
        result.FirstResponseCompliance.Breached.Should().Be(0);
        result.FirstResponseCompliance.Rate.Should().BeNull();

        result.ResolutionCompliance.Achieved.Should().Be(0);
        result.ResolutionCompliance.Breached.Should().Be(0);
        result.ResolutionCompliance.Rate.Should().BeNull();

        result.ResolutionEligibleCount.Should().Be(0);
        result.AverageResolutionMinutes.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenSampleDurationIsZero_ReturnsZeroAverage()
    {
        var useCase = CreateUseCase();
        var queryResult = new TicketAnalyticsQueryResult(
            Volume: 1,
            StatusDistribution: new Dictionary<string, int>(),
            PriorityDistribution: new Dictionary<string, int>(),
            FirstResponseAchieved: 1,
            FirstResponseBreached: 0,
            ResolutionAchieved: 1,
            ResolutionBreached: 0,
            ResolutionEligibleCount: 1,
            ResolutionTotalMinutes: 0.0
        );

        _query.Setup(q => q.QueryAnalyticsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);

        var result = await useCase.ExecuteAsync(new("2026-11-01T00:00:00Z", "2026-11-15T00:00:00Z"));

        result.ResolutionEligibleCount.Should().Be(1);
        result.AverageResolutionMinutes.Should().Be(0.0);
    }

    private static TicketAnalyticsQueryResult CreateEmptyResult() =>
        new(0, new Dictionary<string, int>(), new Dictionary<string, int>(), 0, 0, 0, 0, 0, 0.0);
}
