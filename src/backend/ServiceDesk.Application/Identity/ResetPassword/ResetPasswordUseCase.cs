using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Identity.Common;
using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Identity.Ports;

namespace ServiceDesk.Application.Identity.ResetPassword;

public interface IResetPassword
{
    Task<IdentityOperationResult> ExecuteAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
}

internal sealed class ResetPasswordUseCase(
    IOtpCodeStore otpStore,
    IUserAccountGateway accounts,
    IRefreshTokenStore refreshTokens,
    IClock clock) : IResetPassword
{
    public async Task<IdentityOperationResult> ExecuteAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.OtpCode) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return IdentityOperationResult.Failed("Email, OTP code and new password are required.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var now = clock.UtcNow.UtcDateTime;
        var codeHash = TokenHasher.Hash(request.OtpCode.Trim());

        var validOtp = await otpStore.FindValidOtpAsync(normalizedEmail, "ResetPassword", codeHash, now, cancellationToken);
        if (validOtp is null)
        {
            return IdentityOperationResult.Failed("Mã OTP không hợp lệ hoặc đã hết hạn.");
        }

        var resetResult = await accounts.ResetPasswordWithOtpAsync(normalizedEmail, request.NewPassword, cancellationToken);
        if (!resetResult.Succeeded)
        {
            return resetResult;
        }

        await otpStore.MarkUsedAsync(validOtp.Id, cancellationToken);

        var user = await accounts.CheckPasswordAsync(normalizedEmail, request.NewPassword, cancellationToken);
        if (user is not null)
        {
            await refreshTokens.RevokeAllForUserAsync(user.UserId, now, cancellationToken);
        }

        return IdentityOperationResult.Success();
    }
}
