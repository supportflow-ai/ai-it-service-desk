using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Identity.Common;
using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Identity.Ports;

namespace ServiceDesk.Application.Identity.RevokeToken;

public interface IRevokeToken
{
    Task ExecuteAsync(RevokeTokenRequest request, CancellationToken cancellationToken = default);
}

internal sealed class RevokeTokenUseCase(
    IRefreshTokenStore refreshTokens,
    IClock clock) : IRevokeToken
{
    public async Task ExecuteAsync(RevokeTokenRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return;
        }

        var now = clock.UtcNow.UtcDateTime;
        var tokenHash = TokenHasher.Hash(request.RefreshToken.Trim());
        await refreshTokens.RevokeTokenAsync(tokenHash, now, cancellationToken);
    }
}
