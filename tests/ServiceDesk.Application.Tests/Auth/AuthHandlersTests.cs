using FluentAssertions;
using ServiceDesk.Application.Auth;
using ServiceDesk.Application.Common.Exceptions;
using ServiceDesk.Application.Common.Interfaces;
using Xunit;

namespace ServiceDesk.Application.Tests.Auth;

public class AuthHandlersTests
{
    private readonly FakeIdentityService _identity = new();
    private readonly FakeTokenService _tokenService = new("test-jwt-token");

    private AuthHandlers CreateHandler(string? currentUserId = null, IReadOnlyList<string>? roles = null, bool isAuthenticated = true)
    {
        var currentUser = new FakeCurrentUser
        {
            UserId = currentUserId,
            Roles = roles ?? [],
            IsAuthenticated = isAuthenticated
        };

        return new AuthHandlers(_identity, _tokenService, currentUser);
    }

    [Fact]
    public async Task Register_AssignsRequesterAndReturnsToken()
    {
        var handler = CreateHandler();

        var result = await handler.RegisterAsync(new RegisterRequest("user@example.com", "ValidPass1"), CancellationToken.None);

        result.Token.Should().Be("test-jwt-token");
        result.User.Email.Should().Be("user@example.com");
        result.User.Roles.Should().ContainSingle(ApplicationRoles.Requester);
    }

    [Theory]
    [InlineData("", "ValidPass1")]
    [InlineData("user@example.com", "")]
    public async Task Register_EmptyCredentials_ThrowsValidation(string email, string password)
    {
        var handler = CreateHandler();

        var act = () => handler.RegisterAsync(new RegisterRequest(email, password), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenAndUser()
    {
        _identity.AddUser("user-1", "user@example.com", "Password123", [ApplicationRoles.Requester]);
        var handler = CreateHandler();

        var result = await handler.LoginAsync(new LoginRequest("user@example.com", "Password123"), CancellationToken.None);

        result.Token.Should().Be("test-jwt-token");
        result.User.Id.Should().Be("user-1");
        result.User.Email.Should().Be("user@example.com");
        result.User.Roles.Should().ContainSingle(ApplicationRoles.Requester);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ThrowsForbidden()
    {
        _identity.AddUser("user-1", "user@example.com", "Password123", [ApplicationRoles.Requester]);
        var handler = CreateHandler();

        var act = () => handler.LoginAsync(new LoginRequest("user@example.com", "WrongPassword"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Theory]
    [InlineData("", "pass")]
    [InlineData("email@example.com", "")]
    public async Task Login_MissingCredentials_ThrowsValidation(string email, string password)
    {
        var handler = CreateHandler();

        var act = () => handler.LoginAsync(new LoginRequest(email, password), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GetMe_Authenticated_ReturnsCurrentUser()
    {
        _identity.AddUser("user-1", "user@example.com", "Pass", [ApplicationRoles.Requester]);
        var handler = CreateHandler(currentUserId: "user-1", roles: [ApplicationRoles.Requester]);

        var me = await handler.GetMeAsync(CancellationToken.None);

        me.Id.Should().Be("user-1");
        me.Email.Should().Be("user@example.com");
        me.Roles.Should().ContainSingle(ApplicationRoles.Requester);
    }

    [Fact]
    public async Task GetMe_Unauthenticated_ThrowsForbidden()
    {
        var handler = CreateHandler(currentUserId: null, isAuthenticated: false);

        var act = () => handler.GetMeAsync(CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetMe_UserNotFound_ThrowsNotFound()
    {
        var handler = CreateHandler(currentUserId: "missing-user", roles: [ApplicationRoles.Requester]);

        var act = () => handler.GetMeAsync(CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ChangeRole_AdminActor_ChangesTargetRole()
    {
        _identity.AddUser("target-1", "target@example.com", "Pass", [ApplicationRoles.Requester]);
        var handler = CreateHandler(currentUserId: "admin-1", roles: [ApplicationRoles.Admin]);

        await handler.ChangeRoleAsync("target-1", ApplicationRoles.Agent, CancellationToken.None);

        var updated = await _identity.GetUserAsync("target-1", CancellationToken.None);
        updated!.Roles.Should().ContainSingle(ApplicationRoles.Agent);
    }

    [Fact]
    public async Task ChangeRole_NonAdminActor_ThrowsForbidden()
    {
        _identity.AddUser("target-1", "target@example.com", "Pass", [ApplicationRoles.Requester]);
        var handler = CreateHandler(currentUserId: "requester-1", roles: [ApplicationRoles.Requester]);

        var act = () => handler.ChangeRoleAsync("target-1", ApplicationRoles.Agent, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ChangeRole_UnknownRole_ThrowsValidation()
    {
        _identity.AddUser("target-1", "target@example.com", "Pass", [ApplicationRoles.Requester]);
        var handler = CreateHandler(currentUserId: "admin-1", roles: [ApplicationRoles.Admin]);

        var act = () => handler.ChangeRoleAsync("target-1", "SuperUser", CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ChangeRole_TargetNotFound_ThrowsNotFound()
    {
        var handler = CreateHandler(currentUserId: "admin-1", roles: [ApplicationRoles.Admin]);

        var act = () => handler.ChangeRoleAsync("missing-target", ApplicationRoles.Agent, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        private readonly Dictionary<string, (string Email, string Password, List<string> Roles)> _users = new();

        public void AddUser(string id, string email, string password, IEnumerable<string> roles)
        {
            _users[id] = (email, password, roles.ToList());
        }

        public Task<IdentityUserRecord> RegisterRequesterAsync(string email, string password, CancellationToken cancellationToken)
        {
            if (_users.Values.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ValidationException("A user with this email already exists.");
            }

            var id = Guid.NewGuid().ToString();
            _users[id] = (email, password, [ApplicationRoles.Requester]);
            return Task.FromResult(new IdentityUserRecord(id, email, [ApplicationRoles.Requester]));
        }

        public Task<IdentityUserRecord?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken)
        {
            var pair = _users.FirstOrDefault(u => u.Value.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (pair.Key is null || pair.Value.Password != password)
                return Task.FromResult<IdentityUserRecord?>(null);

            return Task.FromResult<IdentityUserRecord?>(new IdentityUserRecord(pair.Key, pair.Value.Email, pair.Value.Roles.ToList()));
        }

        public Task<IdentityUserRecord?> GetUserAsync(string userId, CancellationToken cancellationToken)
        {
            if (!_users.TryGetValue(userId, out var user))
                return Task.FromResult<IdentityUserRecord?>(null);

            return Task.FromResult<IdentityUserRecord?>(new IdentityUserRecord(userId, user.Email, user.Roles.ToList()));
        }

        public Task<bool> IsInRoleAsync(string userId, string role, CancellationToken cancellationToken)
        {
            if (!_users.TryGetValue(userId, out var user))
                return Task.FromResult(false);

            return Task.FromResult(user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase));
        }

        public Task SetRoleAsync(string userId, string role, CancellationToken cancellationToken)
        {
            if (!_users.TryGetValue(userId, out var user))
                throw new NotFoundException($"User '{userId}' not found.");

            _users[userId] = (user.Email, user.Password, [role]);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTokenService : ITokenService
    {
        private readonly string _token;

        public FakeTokenService(string token)
        {
            _token = token;
        }

        public string Create(IdentityUserRecord user) => _token;
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public string? UserId { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public IReadOnlyList<string> Roles { get; set; } = [];

        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
