using ServiceDesk.Application.Identity.Models;

namespace ServiceDesk.Application.Identity.Interfaces;

/// <summary>
/// Application abstraction for authentication and user management operations.
/// </summary>
public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<UserInfo?> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default);
}
