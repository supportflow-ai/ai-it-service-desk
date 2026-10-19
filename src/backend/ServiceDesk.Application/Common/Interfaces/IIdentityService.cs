using ServiceDesk.Application.Auth;

namespace ServiceDesk.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<IdentityUserRecord> RegisterRequesterAsync(string email, string password, CancellationToken cancellationToken);
    Task<IdentityUserRecord?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken);
    Task<IdentityUserRecord?> GetUserAsync(string userId, CancellationToken cancellationToken);
    Task<bool> IsInRoleAsync(string userId, string role, CancellationToken cancellationToken);
    Task SetRoleAsync(string userId, string role, CancellationToken cancellationToken);
}
