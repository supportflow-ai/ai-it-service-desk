using Moq;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.CreateTicket;
using ServiceDesk.Modules.Ticketing.Application.Idempotency;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public sealed class CreateTicketUseCaseTests
{
    private readonly Mock<ITicketWriter> _writer = new(MockBehavior.Strict);
    private readonly Mock<ICurrentUser> _user = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

    public CreateTicketUseCaseTests()
    {
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);
        _user.SetupGet(u => u.UserId).Returns(_requesterId.ToString());
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _user.Setup(u => u.IsInRole(RoleNames.Requester)).Returns(true);
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    [Fact]
    public async Task ExecuteAsync_InitializesSlaWithP3TargetAndStartsResolutionSegment()
    {
        Ticket? capturedTicket = null;
        CreateTicketFingerprint? capturedFingerprint = null;

        _writer.Setup(w => w.CreateAsync(It.IsAny<Ticket>(), It.IsAny<CreateTicketFingerprint?>(), It.IsAny<CancellationToken>()))
            .Callback<Ticket, CreateTicketFingerprint?, CancellationToken>((t, f, _) =>
            {
                capturedTicket = t;
                capturedFingerprint = f;
            })
            .ReturnsAsync(new TicketDto(Guid.NewGuid(), "IT-2026-0001", "VPN issue", "Cannot connect to VPN from home", TicketCategories.VPN, (int)TicketStatus.Submitted, _now.UtcDateTime));

        var useCase = new CreateTicketUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new CreateTicketCommand("VPN issue", "Cannot connect to VPN from home", TicketCategories.VPN);

        var result = await useCase.ExecuteAsync(command);

        Assert.NotNull(result);
        Assert.NotNull(capturedTicket);
        Assert.Null(capturedFingerprint);

        // Kiểm tra SLA đã khởi tạo
        Assert.True(capturedTicket.IsSlaApplicable);
        Assert.Equal(240, capturedTicket.FirstResponseTargetMinutes); // P3: 4h = 240m
        Assert.Equal(1440, capturedTicket.ResolutionTargetMinutes); // P3: 24h = 1440m
        Assert.Null(capturedTicket.FirstRespondedAt);
        Assert.False(capturedTicket.FirstResponseBreached);
        Assert.Equal(TimeSpan.Zero, capturedTicket.ResolutionAccumulatedDuration);
        Assert.Equal(_now.UtcDateTime, capturedTicket.ResolutionSegmentStartedAt);
        Assert.Null(capturedTicket.ResolvedAt);
        Assert.False(capturedTicket.ResolutionBreached);

        _writer.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_WithIdempotencyKey_CreatesFingerprint()
    {
        CreateTicketFingerprint? capturedFingerprint = null;

        _writer.Setup(w => w.CreateAsync(It.IsAny<Ticket>(), It.IsAny<CreateTicketFingerprint?>(), It.IsAny<CancellationToken>()))
            .Callback<Ticket, CreateTicketFingerprint?, CancellationToken>((_, f, _) => capturedFingerprint = f)
            .ReturnsAsync(new TicketDto(Guid.NewGuid(), "IT-2026-0001", "VPN issue", "Cannot connect to VPN from home", TicketCategories.VPN, (int)TicketStatus.Submitted, _now.UtcDateTime));

        var useCase = new CreateTicketUseCase(_writer.Object, _user.Object, _clock.Object);
        var command = new CreateTicketCommand("VPN issue", "Cannot connect to VPN from home", TicketCategories.VPN);

        var result = await useCase.ExecuteAsync(command, "idemp-key-123");

        Assert.NotNull(result);
        Assert.NotNull(capturedFingerprint);
        Assert.NotEmpty(capturedFingerprint.KeyHash);
        Assert.NotEmpty(capturedFingerprint.PayloadHash);
        Assert.NotEqual(0L, capturedFingerprint.LockId);

        _writer.VerifyAll();
    }
}
