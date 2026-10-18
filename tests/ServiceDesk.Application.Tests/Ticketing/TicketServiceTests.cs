using Moq;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing;
using ServiceDesk.Application.Ticketing.Commands;
using ServiceDesk.Application.Ticketing.Dtos;
using ServiceDesk.Application.Ticketing.Idempotency;
using ServiceDesk.Application.Ticketing.Interfaces;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Ticketing;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public sealed class TicketServiceTests
{
    private readonly Mock<ITicketWriter> _writer = new(MockBehavior.Strict);
    private readonly Mock<ITicketQueries> _queries = new(MockBehavior.Strict);
    private readonly Mock<ICurrentUser> _user = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Guid _actor = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 10, 18, 1, 0, 0, TimeSpan.Zero);
    private readonly TicketService _service;
    private static readonly CreateTicketCommand Valid = new("  VPN issue  ", "  Cannot connect to the corporate VPN.  ", "VPN");

    public TicketServiceTests()
    {
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);
        _user.SetupGet(u => u.UserId).Returns(_actor.ToString());
        _user.SetupGet(u => u.Roles).Returns([RoleNames.Requester]);
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _service = new(_writer.Object, _queries.Object, _user.Object, _clock.Object);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("sample-key")]
    public async Task Create_NormalizesDomainInputAndDelegatesOneAtomicOperation(string? key)
    {
        Ticket? captured = null;
        CreateTicketFingerprint? fingerprint = null;
        _writer.Setup(w => w.CreateAsync(It.IsAny<Ticket>(), It.IsAny<CreateTicketFingerprint?>(), It.IsAny<CancellationToken>()))
            .Callback<Ticket, CreateTicketFingerprint?, CancellationToken>((t, f, _) => { captured = t; fingerprint = f; })
            .ReturnsAsync((Ticket t, CreateTicketFingerprint? _, CancellationToken _) =>
                new TicketDto(t.Id, "IT-2026-0001", t.Title, t.Description, t.CategoryId, (int)t.Status, t.CreatedAt, t.RequesterDepartmentId));
        var result = await _service.CreateTicketAsync(Valid, key);
        Assert.Equal(_actor, captured!.RequesterId);
        Assert.Equal(_now.UtcDateTime, captured.CreatedAt);
        Assert.Equal("VPN issue", result.Title);
        Assert.Equal("Cannot connect to the corporate VPN.", result.Description);
        Assert.Equal((int)TicketStatus.Submitted, result.Status);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(key is not null, fingerprint is not null);
        if (key is not null) Assert.Equal(CreateTicketFingerprint.Create(_actor, key, Valid), fingerprint);
        _writer.VerifyAll();
        _writer.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("", "Valid description", "NET")]
    [InlineData("   ", "Valid description", "NET")]
    [InlineData("Valid", "short", "NET")]
    [InlineData("Valid", "", "NET")]
    [InlineData("Valid", "Valid description", "INVALID")]
    public async Task InvalidDomainInput_DoesNotReachPersistence(string title, string description, string category)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateTicketAsync(new(title, description, category)));
        _writer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TitleOver100Characters_IsRejectedBeforePersistence() =>
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateTicketAsync(Valid with { Title = new string('x', 101) }));

    [Theory]
    [InlineData(0, 10)] [InlineData(1, 0)] [InlineData(1, 101)] [InlineData(int.MaxValue, 100)]
    public async Task InvalidPaging_IsRejected(int page, int pageSize)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetMyTicketsAsync(page, pageSize));
        _queries.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task List_UsesAuthenticatedActorAndPreservesPagedResult()
    {
        var result = new PagedTicketResult([], 2, 5, 12);
        _queries.Setup(q => q.GetForRequesterAsync(_actor, 2, 5, It.IsAny<CancellationToken>())).ReturnsAsync(result);
        Assert.Same(result, await _service.GetMyTicketsAsync(2, 5));
        _queries.VerifyAll();
    }

    [Theory]
    [InlineData(RoleNames.Requester, false)] [InlineData(RoleNames.Agent, true)] [InlineData(RoleNames.Admin, true)]
    public async Task Detail_RestrictsRequesterAndAllowsAgentAdmin(string role, bool broad)
    {
        _user.Setup(u => u.IsInRole(role)).Returns(true);
        var id = Guid.NewGuid();
        _queries.Setup(q => q.GetByIdAsync(id, broad ? null : _actor, It.IsAny<CancellationToken>())).ReturnsAsync((TicketDto?)null);
        Assert.Null(await _service.GetTicketByIdAsync(id));
        _queries.VerifyAll();
    }

    [Theory]
    [InlineData("anonymous")] [InlineData("missing")] [InlineData("invalid")] [InlineData("empty")] [InlineData("no-role")]
    public async Task InvalidActor_CannotReachAnyUseCase(string mode)
    {
        _user.SetupGet(u => u.IsAuthenticated).Returns(mode != "anonymous");
        _user.SetupGet(u => u.UserId).Returns(mode switch { "missing" => null, "invalid" => "bad", "empty" => Guid.Empty.ToString(), _ => _actor.ToString() });
        if (mode == "no-role") _user.SetupGet(u => u.Roles).Returns([]);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CreateTicketAsync(Valid));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CreateTicketAsync(Valid, "key"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.GetMyTicketsAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.GetTicketByIdAsync(Guid.NewGuid()));
        _writer.VerifyNoOtherCalls(); _queries.VerifyNoOtherCalls();
    }
}
