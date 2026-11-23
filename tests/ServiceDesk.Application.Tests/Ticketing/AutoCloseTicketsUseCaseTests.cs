using Moq;
using ServiceDesk.Modules.Ticketing.Application.AutoCloseTickets;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public class AutoCloseTicketsUseCaseTests
{
    private readonly Mock<IAutoCloseTicketsGateway> _gateway = new();
    private readonly Mock<IClock> _clock = new();
    private readonly DateTimeOffset _now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    public AutoCloseTicketsUseCaseTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    private Ticket CreateResolvedTicket(DateTime resolvedAt)
    {
        var agentId = Guid.NewGuid();
        var ticket = Ticket.Create(Guid.NewGuid(), "Resolved Ticket", "Description", TicketCategories.SW, resolvedAt.AddDays(-5));
        ticket.Claim(agentId, resolvedAt.AddDays(-3));
        ticket.Resolve(resolvedAt);
        return ticket;
    }

    [Fact]
    public async Task AutoClose_ClosesEligibleTicketsAndRecordsAudit()
    {
        var resolvedAt = _now.UtcDateTime.AddHours(-73);
        var ticket = CreateResolvedTicket(resolvedAt);

        _gateway.SetupSequence(g => g.GetResolvedTicketsEligibleForAutoCloseAsync(
                It.IsAny<DateTime>(),
                It.IsAny<Guid?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([ticket])
            .ReturnsAsync([]);

        _gateway.Setup(g => g.CloseWithAuditAsync(ticket, _now.UtcDateTime, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var useCase = new AutoCloseTicketsUseCase(_gateway.Object, _clock.Object);
        var result = await useCase.ExecuteAsync();

        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(1, result.ClosedCount);
        Assert.Equal(0, result.ConflictCount);
        _gateway.Verify(g => g.CloseWithAuditAsync(ticket, _now.UtcDateTime, It.Is<string>(s => s.Contains("72 giờ")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AutoClose_WhenConflictOccurs_IncrementsConflictCount()
    {
        var resolvedAt = _now.UtcDateTime.AddHours(-100);
        var ticket = CreateResolvedTicket(resolvedAt);

        _gateway.SetupSequence(g => g.GetResolvedTicketsEligibleForAutoCloseAsync(
                It.IsAny<DateTime>(),
                It.IsAny<Guid?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([ticket])
            .ReturnsAsync([]);

        _gateway.Setup(g => g.CloseWithAuditAsync(ticket, _now.UtcDateTime, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var useCase = new AutoCloseTicketsUseCase(_gateway.Object, _clock.Object);
        var result = await useCase.ExecuteAsync();

        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(0, result.ClosedCount);
        Assert.Equal(1, result.ConflictCount);
    }
}
