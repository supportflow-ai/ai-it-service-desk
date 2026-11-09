using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Domain;

namespace ServiceDesk.Modules.Identity.Infrastructure;

internal sealed class PostgresOtpCodeStore(DbContext context) : IOtpCodeStore
{
    public async Task<bool> TryIssueAsync(OtpCode otp, int maxIssued, DateTime since, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(otp);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        // Khóa tư vấn theo đích + mục đích: các yêu cầu cùng email chạy lần lượt qua đếm → vô hiệu → thêm.
        var scope = "otp:" + otp.Purpose + ":" + otp.Target;
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({scope}, 0))", cancellationToken);

        var issued = await context.Set<OtpCode>()
            .CountAsync(o => o.Target == otp.Target && o.Purpose == otp.Purpose && o.CreatedAt >= since, cancellationToken);
        if (issued >= maxIssued)
        {
            return false;
        }

        // Chỉ giữ một mã còn hiệu lực cho mỗi đích và mục đích.
        await context.Set<OtpCode>()
            .Where(o => o.Target == otp.Target && o.Purpose == otp.Purpose && !o.IsUsed)
            .ExecuteUpdateAsync(setters => setters.SetProperty(o => o.IsUsed, true), cancellationToken);
        context.Set<OtpCode>().Add(otp);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
