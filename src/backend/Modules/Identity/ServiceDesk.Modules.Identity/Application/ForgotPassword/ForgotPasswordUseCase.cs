using System.Security.Cryptography;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Identity.Application.Common;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Domain;

namespace ServiceDesk.Modules.Identity.Application.ForgotPassword;

public interface IForgotPassword
{
    Task<IdentityOperationResult> ExecuteAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Phát mã OTP đặt lại mật khẩu:
/// - Luôn trả thành công để không lộ email nào đã đăng ký.
/// - Chỉ phát mã khi email tồn tại và chưa vượt <see cref="OtpCode.MaxIssuedPerHour"/> mã trong một giờ.
/// - Mã mới vô hiệu các mã cũ; chỉ giá trị băm được lưu, mã gốc chỉ đi qua cổng gửi.
/// </summary>
internal sealed class ForgotPasswordUseCase(
    IUserAccountGateway accounts,
    IOtpCodeStore otpStore,
    IOtpSender sender,
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
        var now = clock.UtcNow.UtcDateTime;
        if (!await accounts.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            return IdentityOperationResult.Success();
        }

        var issuedLastHour = await otpStore.CountIssuedSinceAsync(
            normalizedEmail, OtpCode.ResetPasswordPurpose, now.AddHours(-1), cancellationToken);
        if (issuedLastHour >= OtpCode.MaxIssuedPerHour)
        {
            return IdentityOperationResult.Success();
        }

        var rawOtp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var otp = OtpCode.Issue(normalizedEmail, OtpCode.ResetPasswordPurpose, TokenHasher.Hash(rawOtp), now);
        await otpStore.IssueAsync(otp, cancellationToken);
        await sender.SendPasswordResetCodeAsync(normalizedEmail, rawOtp, otp.ExpiresAt, cancellationToken);

        return IdentityOperationResult.Success();
    }
}
