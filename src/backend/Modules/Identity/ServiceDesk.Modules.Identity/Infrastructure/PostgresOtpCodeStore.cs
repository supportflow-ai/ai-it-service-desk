using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Domain;

namespace ServiceDesk.Modules.Identity.Infrastructure;

internal sealed class PostgresOtpCodeStore(DbContext context) : IOtpCodeStore
{
    public async Task IssueAsync(OtpCode otp, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(otp);
        // Chỉ giữ một mã còn hiệu lực cho mỗi đích và mục đích.
        await context.Set<OtpCode>()
            .Where(o => o.Target == otp.Target && o.Purpose == otp.Purpose && !o.IsUsed)
            .ExecuteUpdateAsync(setters => setters.SetProperty(o => o.IsUsed, true), cancellationToken);
        context.Set<OtpCode>().Add(otp);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CountIssuedSinceAsync(string target, string purpose, DateTime since, CancellationToken cancellationToken = default)
    {
        return context.Set<OtpCode>().CountAsync(o => o.Target == target && o.Purpose == purpose && o.CreatedAt >= since, cancellationToken);
    }
}
