using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Domain;

namespace ServiceDesk.Modules.Identity.Infrastructure;

internal sealed class PostgresRefreshTokenStore(DbContext context) : IRefreshTokenStore
{
    public async Task SaveTokenAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        context.Set<RefreshToken>().Add(token);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<RefreshToken?> FindActiveByHashAsync(string tokenHash, DateTime now, CancellationToken cancellationToken = default)
    {
        return context.Set<RefreshToken>()
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash && r.RevokedAt == null && r.ExpiresAt > now, cancellationToken);
    }

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return context.Set<RefreshToken>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<bool> TryRevokeAsync(Guid tokenId, DateTime revokedAt, CancellationToken cancellationToken = default)
    {
        // Một câu UPDATE có điều kiện: chỉ một request thắng khi nhiều request dùng cùng token.
        var affected = await context.Set<RefreshToken>()
            .Where(r => r.Id == tokenId && r.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.RevokedAt, revokedAt), cancellationToken);
        return affected == 1;
    }

    public async Task RevokeTokenAsync(string tokenHash, DateTime revokedAt, CancellationToken cancellationToken = default)
    {
        var token = await context.Set<RefreshToken>()
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash && r.RevokedAt == null, cancellationToken);
        if (token is not null)
        {
            token.RevokedAt = revokedAt;
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeAllForUserAsync(string userId, DateTime revokedAt, CancellationToken cancellationToken = default)
    {
        var activeTokens = await context.Set<RefreshToken>()
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
