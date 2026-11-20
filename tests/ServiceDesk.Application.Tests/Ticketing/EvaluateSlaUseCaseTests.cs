using Moq;
using ServiceDesk.Modules.Ticketing.Application.EvaluateSla;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public sealed class EvaluateSlaUseCaseTests
{
    private readonly Mock<ISlaEvaluationGateway> _gateway = new(MockBehavior.Strict);
    private readonly Mock<IClock> _clock = new();
    private readonly DateTimeOffset _baseTime = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

    public EvaluateSlaUseCaseTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_baseTime);
    }

    private Ticket CreateTicketWithSla(DateTime createdAt, Priority priority = Priority.P3)
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Title test", "Description test at least 10 chars", TicketCategories.VPN, createdAt);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(priority), createdAt);
        return ticket;
    }

    [Fact]
    public void EvaluateSlaBreach_BeforeDeadline_DoesNotBreach()
    {
        // Target P3: First Response 240m (4h), Resolution 1440m (24h)
        var createdAt = _baseTime.UtcDateTime;
        var ticket = CreateTicketWithSla(createdAt);

        // Đánh giá tại mốc +239 phút (chưa đến hạn)
        var evalTime = createdAt.AddMinutes(239);
        var changed = ticket.EvaluateSlaBreach(evalTime);

        Assert.False(changed);
        Assert.False(ticket.FirstResponseBreached);
        Assert.False(ticket.ResolutionBreached);
    }

    [Fact]
    public void EvaluateSlaBreach_AtExactDeadline_DoesNotBreach_E_Equals_T()
    {
        // Quy tắc: E = T vẫn trong hạn, chưa breach
        var createdAt = _baseTime.UtcDateTime;
        var ticket = CreateTicketWithSla(createdAt);

        // Đánh giá đúng mốc +240 phút
        var evalTime = createdAt.AddMinutes(240);
        var changed = ticket.EvaluateSlaBreach(evalTime);

        Assert.False(changed);
        Assert.False(ticket.FirstResponseBreached);
        Assert.False(ticket.ResolutionBreached);
    }

    [Fact]
    public void EvaluateSlaBreach_AfterDeadline_Breaches_E_Greater_Than_T()
    {
        // Quy tắc: E > T là breach
        var createdAt = _baseTime.UtcDateTime;
        var ticket = CreateTicketWithSla(createdAt);

        // Đánh giá tại mốc +240 phút và 1 microsecond (vượt quá hạn)
        var evalTime = createdAt.AddMinutes(240).AddTicks(10);
        var changed = ticket.EvaluateSlaBreach(evalTime);

        Assert.True(changed);
        Assert.True(ticket.FirstResponseBreached);
        Assert.False(ticket.ResolutionBreached); // Resolution chưa đến hạn 1440m
    }

    [Fact]
    public void EvaluateSlaBreach_ResolutionBreach_WhenElapsedExceedsTarget()
    {
        var createdAt = _baseTime.UtcDateTime;
        var ticket = CreateTicketWithSla(createdAt);

        // Phản hồi lúc 1h (không breach first response)
        ticket.RecordFirstResponse(createdAt.AddHours(1));

        // Đánh giá tại mốc 1440m + 1s -> Resolution breach
        var evalTime = createdAt.AddMinutes(1440).AddSeconds(1);
        var changed = ticket.EvaluateSlaBreach(evalTime);

        Assert.True(changed);
        Assert.False(ticket.FirstResponseBreached);
        Assert.True(ticket.ResolutionBreached);
    }

    [Fact]
    public void EvaluateSlaBreach_InPendingUser_FirstResponseCanBreach_WhileResolutionIsPaused()
    {
        var createdAt = _baseTime.UtcDateTime;
        var ticket = CreateTicketWithSla(createdAt);
        ticket.Claim(Guid.NewGuid(), createdAt.AddMinutes(10));

        // Tạm dừng sau 30 phút làm việc
        ticket.MarkPending(TicketStatus.PendingUser, createdAt.AddMinutes(30));

        // Đánh giá tại mốc +5 tiếng (300 phút) sau tạo
        // First Response: không pause, 300m > 240m -> Breach!
        // Resolution: đã pause tại 30 phút làm việc -> 30m < 1440m -> KHÔNG breach!
        var evalTime = createdAt.AddMinutes(300);
        var changed = ticket.EvaluateSlaBreach(evalTime);

        Assert.True(changed);
        Assert.True(ticket.FirstResponseBreached);
        Assert.False(ticket.ResolutionBreached);
        Assert.Equal(TimeSpan.FromMinutes(30), ticket.ResolutionAccumulatedDuration);
    }

    [Fact]
    public void EvaluateSlaBreach_CompletedOrCancelledOrLegacy_DoesNotBreach()
    {
        var createdAt = _baseTime.UtcDateTime;

        // Legacy ticket không áp dụng SLA
        var legacyTicket = Ticket.Create(Guid.NewGuid(), "Legacy", "Legacy description long enough", TicketCategories.VPN, createdAt);
        var legacyChanged = legacyTicket.EvaluateSlaBreach(createdAt.AddDays(10));
        Assert.False(legacyChanged);
        Assert.False(legacyTicket.FirstResponseBreached);
        Assert.False(legacyTicket.ResolutionBreached);

        // Cancelled ticket
        var cancelledTicket = CreateTicketWithSla(createdAt);
        cancelledTicket.Cancel(createdAt.AddMinutes(30));
        var cancelChanged = cancelledTicket.EvaluateSlaBreach(createdAt.AddDays(10));
        Assert.False(cancelChanged);
    }

    [Fact]
    public async Task ExecuteAsync_KeysetPagination_TraversesMultipleBatches_AndUpdatesBreachedOnly()
    {
        var now = _baseTime.UtcDateTime;
        _clock.SetupGet(c => c.UtcNow).Returns(_baseTime);

        // Batch 1: 2 ticket (ticket 1 breach, ticket 2 chưa breach)
        var t1 = CreateTicketWithSla(now.AddHours(-5)); // > 4h -> breach FR
        var t2 = CreateTicketWithSla(now.AddHours(-1)); // 1h < 4h -> no breach
        // Batch 2: 1 ticket (breach resolution)
        var t3 = CreateTicketWithSla(now.AddHours(-30)); // > 24h -> breach FR and Res

        _gateway.Setup(g => g.GetActiveSlaTicketsBatchAsync(null, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Ticket> { t1, t2 });

        _gateway.Setup(g => g.GetActiveSlaTicketsBatchAsync(t2.Id, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Ticket> { t3 });

        _gateway.Setup(g => g.GetActiveSlaTicketsBatchAsync(t3.Id, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Ticket>()); // Hết

        _gateway.Setup(g => g.UpdateBreachedMarkersAsync(t1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _gateway.Setup(g => g.UpdateBreachedMarkersAsync(t3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var useCase = new EvaluateSlaUseCase(_gateway.Object, _clock.Object, batchSize: 2);
        var result = await useCase.ExecuteAsync();

        Assert.Equal(3, result.ProcessedCount);
        Assert.Equal(2, result.UpdatedCount);
        Assert.Equal(0, result.ConflictCount);

        _gateway.Verify(g => g.UpdateBreachedMarkersAsync(t1, It.IsAny<CancellationToken>()), Times.Once);
        _gateway.Verify(g => g.UpdateBreachedMarkersAsync(t2, It.IsAny<CancellationToken>()), Times.Never);
        _gateway.Verify(g => g.UpdateBreachedMarkersAsync(t3, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenConflictOccurs_TracksConflictAndContinues()
    {
        var now = _baseTime.UtcDateTime;
        _clock.SetupGet(c => c.UtcNow).Returns(_baseTime);

        var t1 = CreateTicketWithSla(now.AddHours(-5));
        var t2 = CreateTicketWithSla(now.AddHours(-5));

        _gateway.Setup(g => g.GetActiveSlaTicketsBatchAsync(null, EvaluateSlaUseCase.DefaultBatchSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Ticket> { t1, t2 });

        _gateway.Setup(g => g.GetActiveSlaTicketsBatchAsync(t2.Id, EvaluateSlaUseCase.DefaultBatchSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Ticket>());

        // t1 bị conflict (ví dụ concurrency xmin), t2 thành công
        _gateway.Setup(g => g.UpdateBreachedMarkersAsync(t1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _gateway.Setup(g => g.UpdateBreachedMarkersAsync(t2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var useCase = new EvaluateSlaUseCase(_gateway.Object, _clock.Object);
        var result = await useCase.ExecuteAsync();

        Assert.Equal(2, result.ProcessedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.ConflictCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var now = _baseTime.UtcDateTime;
        _clock.SetupGet(c => c.UtcNow).Returns(_baseTime);

        var t1 = CreateTicketWithSla(now.AddHours(-5));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _gateway.Setup(g => g.GetActiveSlaTicketsBatchAsync(null, EvaluateSlaUseCase.DefaultBatchSize, cts.Token))
            .ReturnsAsync(new List<Ticket> { t1 });

        var useCase = new EvaluateSlaUseCase(_gateway.Object, _clock.Object);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => useCase.ExecuteAsync(cts.Token));
    }
}
