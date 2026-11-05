using System.Security.Cryptography;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Identity.Common;
using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Identity.Ports;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Identity.ForgotPassword;

public interface IForgotPassword
{
    Task<IdentityOperationResult> ExecuteAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
}

internal sealed class ForgotPasswordUseCase(
    IOtpCodeStore otpStore,
    IClock clock) : IForgotPassword
{
    public async Task<IdentityOperationResult> ExecuteAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return IdentityOperationResult.Failed("Email is required.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var rawOtp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var now = clock.UtcNow.UtcDateTime;

        var otpEntity = new OtpCode
        {
            Id = Guid.NewGuid(),
            Target = normalizedEmail,
            CodeHash = TokenHasher.Hash(rawOtp),
            Purpose = "ResetPassword",
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(15),
            IsUsed = false
        };

        await otpStore.SaveOtpAsync(otpEntity, cancellationToken);

        // Always return success to protect user enumeration
        return IdentityOperationResult.Success();
    }
}
