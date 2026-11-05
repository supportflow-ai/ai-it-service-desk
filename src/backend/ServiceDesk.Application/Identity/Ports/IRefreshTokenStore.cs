using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Identity.Ports;

public interface IRefreshTokenStore
{
    Task SaveTokenAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task<RefreshToken?> FindActiveByHashAsync(string tokenHash, DateTime now, CancellationToken cancellationToken = default);
    Task RevokeTokenAsync(string tokenHash, DateTime revokedAt, CancellationToken cancellationToken = default);
    Task RevokeAllForUserAsync(string userId, DateTime revokedAt, CancellationToken cancellationToken = default);
}
