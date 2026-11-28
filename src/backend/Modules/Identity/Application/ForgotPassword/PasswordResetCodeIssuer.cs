using System.Security.Cryptography;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Domain;

namespace ServiceDesk.Modules.Identity.Application.ForgotPassword;

/// <summary>Phát mã và outbox nguyên tử; retry gửi lại cùng mã đã mã hóa, không phát mã mới.</summary>
internal sealed class PasswordResetCodeIssuer(
    IUserAccountGateway accounts,
    IPasswordResetJobStore jobs,
    IOtpSender sender,
    IClock clock,
    IOtpCodeProtector protector)
{
    public async Task ProcessAsync(PasswordResetJob claim, CancellationToken cancellationToken = default)
    {
        if (claim.ProtectedCode is null)
        {
            if (!await accounts.EmailExistsAsync(claim.Email, cancellationToken))
            {
                await jobs.CompleteAsync(claim, clock.UtcNow.UtcDateTime, "UnknownAccount", cancellationToken);
                return;
            }
            var now = clock.UtcNow.UtcDateTime;
            var raw = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var code = OtpCode.Issue(claim.Email, OtpCode.ResetPasswordPurpose, protector.Hash(claim.Email, OtpCode.ResetPasswordPurpose, raw), now);
            var prepared = await jobs.PrepareAsync(claim, code, protector.Protect(claim.Email, code.Id, raw), now, cancellationToken);
            if (prepared is null)
            {
                await jobs.CompleteAsync(claim, clock.UtcNow.UtcDateTime, "IssuanceLimitOrLeaseLost", cancellationToken);
                return;
            }
            claim = prepared;
        }
        if (!await jobs.RenewForSendAsync(claim, clock.UtcNow.UtcDateTime, cancellationToken)) return;
        var rawCode = protector.Unprotect(claim.Email, claim.OtpCodeId!.Value, claim.ProtectedCode!);
        var remaining = claim.ExpiresAt - clock.UtcNow.UtcDateTime;
        if (remaining <= TimeSpan.Zero) return;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(remaining < TimeSpan.FromSeconds(20) ? remaining : TimeSpan.FromSeconds(20));
        await sender.SendPasswordResetCodeAsync(claim.Email, rawCode, claim.ExpiresAt, timeout.Token);
        await jobs.CompleteAsync(claim, clock.UtcNow.UtcDateTime, cancellationToken: cancellationToken);
    }
}
