using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Identity.Ports;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Infrastructure.Persistence;

namespace ServiceDesk.Infrastructure.Identity;

internal sealed class PostgresOtpCodeStore(ApplicationDbContext context) : IOtpCodeStore
{
    public async Task SaveOtpAsync(OtpCode otp, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(otp);
        context.OtpCodes.Add(otp);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<OtpCode?> FindValidOtpAsync(string target, string purpose, string codeHash, DateTime now, CancellationToken cancellationToken = default)
    {
        return context.OtpCodes
            .FirstOrDefaultAsync(o => o.Target == target && o.Purpose == purpose && o.CodeHash == codeHash && !o.IsUsed && o.ExpiresAt >= now, cancellationToken);
    }

    public async Task MarkUsedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var otp = await context.OtpCodes.FindAsync([id], cancellationToken);
        if (otp is not null)
        {
            otp.IsUsed = true;
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
