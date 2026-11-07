using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Identity.Application.Common;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Ports;

namespace ServiceDesk.Modules.Identity.Application.RevokeToken;

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
