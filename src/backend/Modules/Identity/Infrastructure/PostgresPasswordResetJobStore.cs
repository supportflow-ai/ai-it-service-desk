using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.Identity.Infrastructure;

internal sealed class PostgresPasswordResetJobStore(DbContext context, PostgresOtpCodeStore codes, IClock clock) : IPasswordResetJobStore, IPasswordResetQueue
{
    public async Task<bool> EnqueueAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow.UtcDateTime;
        var job = PasswordResetJob.Request(normalizedEmail, now);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"password-reset-jobs:admission"}, 0))", cancellationToken);
        if (await context.Set<PasswordResetJob>().CountAsync(j => j.CompletedAt == null && j.ExpiresAt > now, cancellationToken) >= PasswordResetJob.Capacity)
            return false;
        context.Set<PasswordResetJob>().Add(job);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<PasswordResetJob?> ClaimAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        await context.Set<PasswordResetJob>().Where(j => j.CompletedAt == null &&
                (j.ExpiresAt <= now || j.Attempts >= PasswordResetJob.MaxAttempts && (j.LeaseUntil == null || j.LeaseUntil <= now)))
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.CompletedAt, now).SetProperty(j => j.ProtectedCode, (string?)null)
                .SetProperty(j => j.FailureCode, "ExpiredOrAttemptsExceeded"), cancellationToken);
        await context.Set<PasswordResetJob>().Where(j => j.CompletedAt < now.AddDays(-1)).Take(100).ExecuteDeleteAsync(cancellationToken);
        var lease = Guid.NewGuid();
        var until = now.AddMinutes(1);
        return (await context.Set<PasswordResetJob>().FromSqlInterpolated($"""
            UPDATE public.password_reset_jobs SET lease_id = {lease}, lease_until = {until}, attempts = attempts + 1
            WHERE id = (SELECT id FROM public.password_reset_jobs
                WHERE completed_at IS NULL AND expires_at > {now} AND next_attempt_at <= {now}
                  AND attempts < {PasswordResetJob.MaxAttempts} AND (lease_until IS NULL OR lease_until <= {now})
                ORDER BY created_at, id FOR UPDATE SKIP LOCKED LIMIT 1)
            RETURNING *
            """).AsNoTracking().ToListAsync(cancellationToken)).SingleOrDefault();
    }

    public async Task<PasswordResetJob?> PrepareAsync(PasswordResetJob claim, OtpCode code, string protectedCode, DateTime now, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var job = (await context.Set<PasswordResetJob>().FromSqlInterpolated($"""
            SELECT * FROM public.password_reset_jobs WHERE id = {claim.Id} AND lease_id = {claim.LeaseId}
                AND completed_at IS NULL AND lease_until > {now} AND expires_at > {now} FOR UPDATE
            """).ToListAsync(cancellationToken)).SingleOrDefault();
        if (job is null) return null;
        if (job.ProtectedCode is not null) return job;
        if (!await codes.TryIssueInTransactionAsync(code, OtpCode.MaxIssuedPerHour, now.AddHours(-1), cancellationToken)) return null;
        job.Prepare(code, protectedCode);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return job;
    }

    public async Task<bool> RenewForSendAsync(PasswordResetJob claim, DateTime now, CancellationToken cancellationToken = default) =>
        await context.Set<PasswordResetJob>().Where(j => j.Id == claim.Id && j.LeaseId == claim.LeaseId && j.CompletedAt == null &&
                j.LeaseUntil > now && j.ExpiresAt > now && context.Set<OtpCode>().Any(o => o.Id == j.OtpCodeId && !o.IsUsed &&
                    o.FailedAttempts < OtpCode.MaxFailedAttempts && o.ExpiresAt > now))
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.LeaseUntil, now.AddMinutes(1)), cancellationToken) == 1;

    public Task CompleteAsync(PasswordResetJob claim, DateTime now, string? failureCode = null, CancellationToken cancellationToken = default) =>
        context.Set<PasswordResetJob>().Where(j => j.Id == claim.Id && j.LeaseId == claim.LeaseId && j.CompletedAt == null && j.LeaseUntil > now)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.CompletedAt, now).SetProperty(j => j.ProtectedCode, (string?)null)
                .SetProperty(j => j.FailureCode, failureCode).SetProperty(j => j.LeaseUntil, (DateTime?)null), cancellationToken);

    public async Task FailAsync(PasswordResetJob claim, DateTime now, bool retry, string failureCode, CancellationToken cancellationToken = default)
    {
        var current = await context.Set<PasswordResetJob>().Where(j => j.Id == claim.Id && j.LeaseId == claim.LeaseId && j.CompletedAt == null && j.LeaseUntil > now)
            .Select(j => new { j.Attempts, j.ExpiresAt }).SingleOrDefaultAsync(cancellationToken);
        if (current is null) return;
        if (!retry || current.Attempts >= PasswordResetJob.MaxAttempts || now >= current.ExpiresAt)
        {
            await CompleteAsync(claim, now, failureCode, cancellationToken);
            return;
        }
        var next = now.AddSeconds(Math.Min(60, 5 * (1 << Math.Min(current.Attempts - 1, 4))));
        await context.Set<PasswordResetJob>().Where(j => j.Id == claim.Id && j.LeaseId == claim.LeaseId && j.CompletedAt == null && j.LeaseUntil > now)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.NextAttemptAt, next).SetProperty(j => j.FailureCode, failureCode)
                .SetProperty(j => j.LeaseId, (Guid?)null).SetProperty(j => j.LeaseUntil, (DateTime?)null), cancellationToken);
    }
}
