using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Identity.Ports;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Infrastructure.Persistence;

namespace ServiceDesk.Infrastructure.Identity;

internal sealed class PostgresRefreshTokenStore(ApplicationDbContext context) : IRefreshTokenStore
{
    public async Task SaveTokenAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<RefreshToken?> FindActiveByHashAsync(string tokenHash, DateTime now, CancellationToken cancellationToken = default)
    {
        return context.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash && r.RevokedAt == null && r.ExpiresAt > now, cancellationToken);
    }

    public async Task RevokeTokenAsync(string tokenHash, DateTime revokedAt, CancellationToken cancellationToken = default)
    {
        var token = await context.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash && r.RevokedAt == null, cancellationToken);
        if (token is not null)
        {
            token.RevokedAt = revokedAt;
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeAllForUserAsync(string userId, DateTime revokedAt, CancellationToken cancellationToken = default)
    {
        var activeTokens = await context.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var t in activeTokens)
        {
            t.RevokedAt = revokedAt;
        }

        if (activeTokens.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
