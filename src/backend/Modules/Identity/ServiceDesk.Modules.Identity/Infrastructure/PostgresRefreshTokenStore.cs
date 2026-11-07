using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Domain;

namespace ServiceDesk.Modules.Identity.Infrastructure;

/// <summary>
/// Kho refresh token trên PostgreSQL. Mọi thao tác ghi lấy <see cref="UserSessionLock"/> trước trong giao dịch của mình;
/// câu UPDATE thu hồi vì vậy luôn bắt đầu sau khi mọi token đang được tạo cho người dùng đã commit.
/// </summary>
internal sealed class PostgresRefreshTokenStore(DbContext context) : IRefreshTokenStore
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return context.Set<RefreshToken>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<bool> TryRotateAsync(Guid currentTokenId, RefreshToken replacement, DateTime now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await UserSessionLock.LockAsync(context, replacement.UserId, cancellationToken);
        // UPDATE có điều kiện: chỉ một request thắng khi nhiều request dùng cùng token.
        var revoked = await context.Set<RefreshToken>()
            .Where(r => r.Id == currentTokenId && r.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.RevokedAt, now), cancellationToken);
        if (revoked != 1)
        {
            return false;
        }

        context.Set<RefreshToken>().Add(replacement);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task RevokeFamilyAsync(string userId, Guid familyId, DateTime revokedAt, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await UserSessionLock.LockAsync(context, userId, cancellationToken);
        await RevokeFamilyInTransactionAsync(familyId, revokedAt, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RevokeSessionByTokenAsync(string tokenHash, DateTime revokedAt, CancellationToken cancellationToken = default)
    {
        var token = await FindByHashAsync(tokenHash, cancellationToken);
        if (token is null)
        {
            return;
        }

        await RevokeFamilyAsync(token.UserId, token.FamilyId, revokedAt, cancellationToken);
    }

    private Task<int> RevokeFamilyInTransactionAsync(Guid familyId, DateTime revokedAt, CancellationToken cancellationToken) =>
        context.Set<RefreshToken>()
            .Where(r => r.FamilyId == familyId && r.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.RevokedAt, revokedAt), cancellationToken);
}
