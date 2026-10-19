using ServiceDesk.Application.Common.Exceptions;
using ServiceDesk.Application.Common.Interfaces;

namespace ServiceDesk.Application.Auth;

public sealed class AuthHandlers
{
    private readonly IIdentityService _identityService;
    private readonly ITokenService _tokenService;
    private readonly ICurrentUser _currentUser;

    public AuthHandlers(
        IIdentityService identityService,
        ITokenService tokenService,
        ICurrentUser currentUser)
    {
        _identityService = identityService;
        _tokenService = tokenService;
        _currentUser = currentUser;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ValidationException("Email is required.");
        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ValidationException("Password is required.");

        var userRecord = await _identityService.RegisterRequesterAsync(request.Email, request.Password, cancellationToken);
        var token = _tokenService.Create(userRecord);

        return new AuthResult(token, new AuthUser(userRecord.Id, userRecord.Email, userRecord.Roles));
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            throw new ValidationException("Email and password are required.");

        var userRecord = await _identityService.ValidateCredentialsAsync(request.Email, request.Password, cancellationToken);
        if (userRecord is null)
            throw new ForbiddenException("Invalid email or password.");

        var token = _tokenService.Create(userRecord);
        return new AuthResult(token, new AuthUser(userRecord.Id, userRecord.Email, userRecord.Roles));
    }

    public async Task<AuthUser> GetMeAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
            throw new ForbiddenException("User is not authenticated.");

        var userRecord = await _identityService.GetUserAsync(_currentUser.UserId, cancellationToken);
        if (userRecord is null)
            throw new NotFoundException("User not found.");

        return new AuthUser(userRecord.Id, userRecord.Email, userRecord.Roles);
    }

    public async Task ChangeRoleAsync(string targetUserId, string newRole, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.IsInRole(ApplicationRoles.Admin))
            throw new ForbiddenException("Only administrators can change user roles.");

        if (string.IsNullOrWhiteSpace(newRole) || !ApplicationRoles.IsValid(newRole))
            throw new ValidationException($"Invalid role '{newRole}'. Allowed roles: {string.Join(", ", ApplicationRoles.All)}.");

        var targetUser = await _identityService.GetUserAsync(targetUserId, cancellationToken);
        if (targetUser is null)
            throw new NotFoundException($"User '{targetUserId}' not found.");

        await _identityService.SetRoleAsync(targetUserId, newRole, cancellationToken);
    }
}
