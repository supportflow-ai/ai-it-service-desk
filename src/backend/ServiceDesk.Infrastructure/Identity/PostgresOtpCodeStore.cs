using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Identity.Ports;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Infrastructure.Persistence;

namespace ServiceDesk.Infrastructure.Identity;

internal sealed class PostgresOtpCodeStore(ApplicationDbContext context) : IOtpCodeStore
{
    public async Task IssueAsync(OtpCode otp, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(otp);
        // Chỉ giữ một mã còn hiệu lực cho mỗi đích và mục đích.
        await context.OtpCodes
            .Where(o => o.Target == otp.Target && o.Purpose == otp.Purpose && !o.IsUsed)
            .ExecuteUpdateAsync(setters => setters.SetProperty(o => o.IsUsed, true), cancellationToken);
        context.OtpCodes.Add(otp);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CountIssuedSinceAsync(string target, string purpose, DateTime since, CancellationToken cancellationToken = default)
    {
        return context.OtpCodes.CountAsync(o => o.Target == target && o.Purpose == purpose && o.CreatedAt >= since, cancellationToken);
    }
}
