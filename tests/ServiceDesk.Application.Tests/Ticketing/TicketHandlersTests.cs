using FluentAssertions;
using ServiceDesk.Application.Auth;
using ServiceDesk.Application.Common.Exceptions;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing;
using ServiceDesk.Domain.Ticketing;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public class TicketHandlersTests
{
    private readonly FakeTicketStore _store = new();
    private readonly FakeIdentityService _identity = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero));

    private TicketHandlers CreateHandlers(string? userId, params string[] roles)
    {
        var currentUser = new FakeCurrentUser
        {
            UserId = userId,
            IsAuthenticated = userId is not null,
            Roles = roles
        };

        return new TicketHandlers(_store, _identity, _clock, currentUser);
    }

    [Fact]
    public async Task Create_ValidInput_CalculatesPriorityAndReturnsSubmittedTicket()
    {
        var handlers = CreateHandlers("requester-1", ApplicationRoles.Requester);

        var response = await handlers.CreateAsync(
            new CreateTicketRequest("VPN Error", "Cannot connect to VPN", "Network", Impact.High, Urgency.Medium),
            CancellationToken.None);

        response.RequesterId.Should().Be("requester-1");
        response.Title.Should().Be("VPN Error");
        response.Priority.Should().Be(Priority.P2);
        response.Status.Should().Be(TicketStatus.Submitted);
        response.AssigneeId.Should().BeNull();
    }

    [Fact]
    public async Task Create_Unauthenticated_ThrowsForbidden()
    {
        var handlers = CreateHandlers(null);

        var act = () => handlers.CreateAsync(
            new CreateTicketRequest("Title", "Desc", "Category", Impact.Low, Urgency.Low),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Theory]
    [InlineData("", "Desc", "Cat")]
    [InlineData("Title", "", "Cat")]
    [InlineData("Title", "Desc", "")]
    public async Task Create_MissingField_ThrowsValidation(string title, string desc, string cat)
    {
        var handlers = CreateHandlers("requester-1", ApplicationRoles.Requester);

        var act = () => handlers.CreateAsync(
            new CreateTicketRequest(title, desc, cat, Impact.Low, Urgency.Low),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Get_OwnerRequester_ReturnsTicket()
    {
        var ticket = Ticket.Create("requester-1", "T", "D", "C", Impact.Low, Urgency.Low, _clock.UtcNow);
        await _store.AddAsync(ticket, CancellationToken.None);

        var handlers = CreateHandlers("requester-1", ApplicationRoles.Requester);
        var response = await handlers.GetAsync(ticket.Id, CancellationToken.None);

        response.Id.Should().Be(ticket.Id);
    }

    [Fact]
    public async Task Get_OtherRequester_ThrowsNotFound_ToPreventExistenceLeak()
    {
        var ticket = Ticket.Create("requester-1", "T", "D", "C", Impact.Low, Urgency.Low, _clock.UtcNow);
        await _store.AddAsync(ticket, CancellationToken.None);

        var handlers = CreateHandlers("requester-2", ApplicationRoles.Requester);
        var act = () => handlers.GetAsync(ticket.Id, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Get_AgentOrAdmin_ReturnsTicketOwnedByAnyRequester()
    {
        var ticket = Ticket.Create("requester-1", "T", "D", "C", Impact.Low, Urgency.Low, _clock.UtcNow);
        await _store.AddAsync(ticket, CancellationToken.None);

        var agentHandlers = CreateHandlers("agent-1", ApplicationRoles.Agent);
        var adminHandlers = CreateHandlers("admin-1", ApplicationRoles.Admin);

        var agentResponse = await agentHandlers.GetAsync(ticket.Id, CancellationToken.None);
        var adminResponse = await adminHandlers.GetAsync(ticket.Id, CancellationToken.None);

        agentResponse.Id.Should().Be(ticket.Id);
        adminResponse.Id.Should().Be(ticket.Id);
    }

    [Fact]
    public async Task List_Requester_ForcesRequesterFilterToSelf()
    {
        var t1 = Ticket.Create("requester-1", "T1", "D", "C", Impact.Low, Urgency.Low, _clock.UtcNow);
        var t2 = Ticket.Create("requester-2", "T2", "D", "C", Impact.Low, Urgency.Low, _clock.UtcNow);
        await _store.AddAsync(t1, CancellationToken.None);
        await _store.AddAsync(t2, CancellationToken.None);

        var handlers = CreateHandlers("requester-1", ApplicationRoles.Requester);
        var response = await handlers.ListAsync(
            new TicketListRequest(null, null, "requester-2", 1, 20),
            CancellationToken.None);

        response.Items.Should().ContainSingle(t => t.Id == t1.Id);
        response.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task List_Agent_SeesWorkspaceAndAppliesFilters()
    {
        var t1 = Ticket.Create("requester-1", "T1", "D", "C", Impact.Low, Urgency.Low, _clock.UtcNow);
        var t2 = Ticket.Create("requester-2", "T2", "D", "C", Impact.Low, Urgency.Low, _clock.UtcNow);
        t2.Claim("agent-2", _clock.UtcNow);

        await _store.AddAsync(t1, CancellationToken.None);
        await _store.AddAsync(t2, CancellationToken.None);

        var handlers = CreateHandlers("agent-1", ApplicationRoles.Agent);
        var response = await handlers.ListAsync(
            new TicketListRequest(TicketStatus.InProgress, "agent-2", null, 1, 20),
            CancellationToken.None);

        response.Items.Should().ContainSingle(t => t.Id == t2.Id);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task List_InvalidPagination_ThrowsValidation(int page, int pageSize)
    {
        var handlers = CreateHandlers("requester-1", ApplicationRoles.Requester);

        var act = () => handlers.ListAsync(new TicketListRequest(null, null, null, page, pageSize), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Update_SubmittedTicket_ByOwner_RecalculatesPriority()
    {
        var ticket = Ticket.Create("requester-1", "Old", "Old", "Old", Impact.Low, Urgency.Low, _clock.UtcNow);
        await _store.AddAsync(ticket, CancellationToken.None);

        var handlers = CreateHandlers("requester-1", ApplicationRoles.Requester);
        var response = await handlers.UpdateAsync(
            ticket.Id,
            new UpdateTicketRequest("New", "New", "New", Impact.High, Urgency.High),
            CancellationToken.None);

        response.Priority.Should().Be(Priority.P1);
        response.Title.Should().Be("New");
    }

    [Fact]
    public async Task Update_NonSubmittedTicket_ThrowsValidation()
    {
        var ticket = Ticket.Create("requester-1", "T", "D", "C", Impact.Low, Urgency.Low, _clock.UtcNow);
        ticket.Claim("agent-1", _clock.UtcNow);
        await _store.AddAsync(ticket, CancellationToken.None);

        var handlers = CreateHandlers("requester-1", ApplicationRoles.Requester);
        var act = () => handlers.UpdateAsync(
            ticket.Id,
            new UpdateTicketRequest("N", "N", "N", Impact.Low, Urgency.Low),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Claim_SubmittedUnassigned_ByAgent_AssignsAndSetsInProgress()
    {
        var ticket = Ticket.Create("requester-1", "T", "D", "C", Impact.Low, Urgency.Low, _clock.UtcNow);
        await _store.AddAsync(ticket, CancellationToken.None);

        var handlers = CreateHandlers("agent-1", ApplicationRoles.Agent);
        var response = await handlers.ClaimAsync(ticket.Id, CancellationToken.None);

        response.Status.Should().Be(TicketStatus.InProgress);
        response.AssigneeId.Should().Be("agent-1");
    }

    [Fact]
    public async Task Claim_SecondAgent_ThrowsConflict()
    {
        var ticket = Ticket.Create("requester-1", "T", "D", "C", Impact.Low, Urgency.Low, _clock.UtcNow);
        await _store.AddAsync(ticket, CancellationToken.None);

        var agent1 = CreateHandlers("agent-1", ApplicationRoles.Agent);
        var agent2 = CreateHandlers("agent-2", ApplicationRoles.Agent);

        await agent1.ClaimAsync(ticket.Id, CancellationToken.None);
        var act = () => agent2.ClaimAsync(ticket.Id, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Claim_ByRequester_ThrowsForbidden()
    {
        var ticket = Ticket.Create("requester-1", "T", "D", "C", Impact.Low, Urgency.Low, _clock.UtcNow);
        await _store.AddAsync(ticket, CancellationToken.None);

        var handlers = CreateHandlers("requester-1", ApplicationRoles.Requester);
        var act = () => handlers.ClaimAsync(ticket.Id, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Assign_ByAdmin_ToAgent_Succeeds()
    {
        var ticket = Ticket.Create("requester-1", "T", "D", "C", Impact.Low, Urgency.Low, _clock.UtcNow);
        await _store.AddAsync(ticket, CancellationToken.None);
        _identity.AddUser("agent-1", "agent1@example.com", "pass", [ApplicationRoles.Agent]);

        var handlers = CreateHandlers("admin-1", ApplicationRoles.Admin);
        var response = await handlers.AssignAsync(ticket.Id, new AssignTicketRequest("agent-1"), CancellationToken.None);

        response.AssigneeId.Should().Be("agent-1");
        response.Status.Should().Be(TicketStatus.InProgress);
    }

    [Fact]
    public async Task Assign_ByAdmin_ToRequester_ThrowsValidation()
    {
        var ticket = Ticket.Create("requester-1", "T", "D", "C", Impact.Low, Urgency.Low, _clock.UtcNow);
        await _store.AddAsync(ticket, CancellationToken.None);
        _identity.AddUser("req-2", "req2@example.com", "pass", [ApplicationRoles.Requester]);

        var handlers = CreateHandlers("admin-1", ApplicationRoles.Admin);
        var act = () => handlers.AssignAsync(ticket.Id, new AssignTicketRequest("req-2"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Resolve_AssignedAgent_Succeeds()
    {
        var ticket = Ticket.Create("requester-1", "T", "D", "C", Impact.Low, Urgency.Low, _clock.UtcNow);
        ticket.Claim("agent-1", _clock.UtcNow);
        await _store.AddAsync(ticket, CancellationToken.None);

        var handlers = CreateHandlers("agent-1", ApplicationRoles.Agent);
        var response = await handlers.ResolveAsync(ticket.Id, CancellationToken.None);

        response.Status.Should().Be(TicketStatus.Resolved);
        response.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Resolve_DifferentAgent_ThrowsForbidden()
    {
        var ticket = Ticket.Create("requester-1", "T", "D", "C", Impact.Low, Urgency.Low, _clock.UtcNow);
        ticket.Claim("agent-1", _clock.UtcNow);
        await _store.AddAsync(ticket, CancellationToken.None);

        var handlers = CreateHandlers("agent-2", ApplicationRoles.Agent);
        var act = () => handlers.ResolveAsync(ticket.Id, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Reopen_And_Close_ByOwnerRequester_Succeeds()
    {
        var ticket = Ticket.Create("requester-1", "T", "D", "C", Impact.Low, Urgency.Low, _clock.UtcNow);
        ticket.Claim("agent-1", _clock.UtcNow);
        ticket.Resolve(_clock.UtcNow);
        await _store.AddAsync(ticket, CancellationToken.None);

        var handlers = CreateHandlers("requester-1", ApplicationRoles.Requester);

        var reopened = await handlers.ReopenAsync(ticket.Id, CancellationToken.None);
        reopened.Status.Should().Be(TicketStatus.InProgress);

        // Re-resolve so we can close
        ticket.Resolve(_clock.UtcNow);

        var closed = await handlers.CloseAsync(ticket.Id, CancellationToken.None);
        closed.Status.Should().Be(TicketStatus.Closed);
        closed.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Cancel_SubmittedTicket_ByOwnerRequester_Succeeds()
    {
        var ticket = Ticket.Create("requester-1", "T", "D", "C", Impact.Low, Urgency.Low, _clock.UtcNow);
        await _store.AddAsync(ticket, CancellationToken.None);

        var handlers = CreateHandlers("requester-1", ApplicationRoles.Requester);
        var response = await handlers.CancelAsync(ticket.Id, CancellationToken.None);

        response.Status.Should().Be(TicketStatus.Cancelled);
        response.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AddComment_And_ListComments_Succeeds()
    {
        var ticket = Ticket.Create("requester-1", "T", "D", "C", Impact.Low, Urgency.Low, _clock.UtcNow);
        await _store.AddAsync(ticket, CancellationToken.None);

        var handlers = CreateHandlers("requester-1", ApplicationRoles.Requester);
        var comment = await handlers.AddCommentAsync(ticket.Id, new AddCommentRequest("Test comment"), CancellationToken.None);

        comment.Content.Should().Be("Test comment");
        comment.AuthorId.Should().Be("requester-1");

        var list = await handlers.ListCommentsAsync(ticket.Id, CancellationToken.None);
        list.Should().ContainSingle(c => c.Content == "Test comment");
    }

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task AddComment_TerminalTicket_ThrowsValidation(TicketStatus terminalStatus)
    {
        var ticket = Ticket.Create("requester-1", "T", "D", "C", Impact.Low, Urgency.Low, _clock.UtcNow);
        if (terminalStatus == TicketStatus.Closed)
        {
            ticket.Claim("agent-1", _clock.UtcNow);
            ticket.Resolve(_clock.UtcNow);
            ticket.Close(_clock.UtcNow);
        }
        else
        {
            ticket.Cancel(_clock.UtcNow);
        }
        await _store.AddAsync(ticket, CancellationToken.None);

        var handlers = CreateHandlers("requester-1", ApplicationRoles.Requester);
        var act = () => handlers.AddCommentAsync(ticket.Id, new AddCommentRequest("Comment on terminal"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    // --- In-memory test fakes ---

    private sealed class FakeTicketStore : ITicketStore
    {
        private readonly List<Ticket> _tickets = [];

        public Task AddAsync(Ticket ticket, CancellationToken cancellationToken)
        {
            _tickets.Add(ticket);
            return Task.CompletedTask;
        }

        public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            var ticket = _tickets.FirstOrDefault(t => t.Id == id);
            return Task.FromResult(ticket);
        }

        public Task<IReadOnlyList<TicketComment>> GetCommentsAsync(Guid ticketId, CancellationToken cancellationToken)
        {
            var ticket = _tickets.FirstOrDefault(t => t.Id == ticketId);
            return Task.FromResult<IReadOnlyList<TicketComment>>(ticket?.Comments ?? []);
        }

        public Task<PagedTickets> ListAsync(TicketListFilter filter, CancellationToken cancellationToken)
        {
            var query = _tickets.AsQueryable();

            if (filter.RequesterId is not null)
                query = query.Where(t => t.RequesterId == filter.RequesterId);
            if (filter.AssigneeId is not null)
                query = query.Where(t => t.AssigneeId == filter.AssigneeId);
            if (filter.Status.HasValue)
                query = query.Where(t => t.Status == filter.Status.Value);

            var total = query.Count();
            var items = query.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToList();

            return Task.FromResult(new PagedTickets(items, total));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        private readonly Dictionary<string, (string Email, string Password, List<string> Roles)> _users = new();

        public void AddUser(string id, string email, string password, params string[] roles)
        {
            _users[id] = (email, password, roles.ToList());
        }

        public Task<IdentityUserRecord> RegisterRequesterAsync(string email, string password, CancellationToken cancellationToken)
        {
            var id = Guid.NewGuid().ToString();
            _users[id] = (email, password, [ApplicationRoles.Requester]);
            return Task.FromResult(new IdentityUserRecord(id, email, [ApplicationRoles.Requester]));
        }

        public Task<IdentityUserRecord?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<IdentityUserRecord?> GetUserAsync(string userId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<bool> IsInRoleAsync(string userId, string role, CancellationToken cancellationToken)
        {
            if (!_users.TryGetValue(userId, out var user))
                return Task.FromResult(false);

            return Task.FromResult(user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase));
        }

        public Task SetRoleAsync(string userId, string role, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public string? UserId { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public IReadOnlyList<string> Roles { get; set; } = [];
        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
