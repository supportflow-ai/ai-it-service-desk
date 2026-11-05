using System.Security.Cryptography;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Identity.Common;
using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Identity.Ports;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Identity.RefreshAuth;

public interface IRefreshAuth
{
    Task<AuthResult> ExecuteAsync(RefreshAuthRequest request, CancellationToken cancellationToken = default);
}

internal sealed class RefreshAuthUseCase(
    IRefreshTokenStore refreshTokens,
    IUserAccountGateway accounts,
    IAccessTokenIssuer tokens,
    IClock clock) : IRefreshAuth
{
    public async Task<AuthResult> ExecuteAsync(RefreshAuthRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return AuthResult.Failed("Refresh token is required.");
        }

        var now = clock.UtcNow.UtcDateTime;
        var tokenHash = TokenHasher.Hash(request.RefreshToken.Trim());
        var activeToken = await refreshTokens.FindActiveByHashAsync(tokenHash, now, cancellationToken);
        if (activeToken is null)
        {
            return AuthResult.Failed("Invalid or expired refresh token.");
        }

        var user = await accounts.FindByIdAsync(activeToken.UserId, cancellationToken);
        if (user is null)
        {
            return AuthResult.Failed("User account no longer exists.");
        }

        // Revoke current token (rotation)
        await refreshTokens.RevokeTokenAsync(tokenHash, now, cancellationToken);

        // Issue new refresh token
        var newRawRefreshToken = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(64));
        var newEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            TokenHash = TokenHasher.Hash(newRawRefreshToken),
            CreatedAt = now,
            ExpiresAt = now.AddDays(7),
            DeviceInfo = activeToken.DeviceInfo,
            IpAddress = activeToken.IpAddress
        };
        await refreshTokens.SaveTokenAsync(newEntity, cancellationToken);

        return AuthResult.Success(tokens.GenerateToken(user), user, newRawRefreshToken);
    }
}
