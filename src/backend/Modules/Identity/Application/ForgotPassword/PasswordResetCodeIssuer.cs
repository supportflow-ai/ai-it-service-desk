using System.Security.Cryptography;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Identity.Application.Common;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Domain;

namespace ServiceDesk.Modules.Identity.Application.ForgotPassword;

/// <summary>
/// Phát mã OTP đặt lại mật khẩu cho một email lấy từ hàng đợi:
/// - Chỉ phát khi email tồn tại và chưa có <see cref="OtpCode.MaxIssuedPerHour"/> mã trong một giờ.
/// - Mã mới vô hiệu các mã cũ; chỉ giá trị băm được lưu, mã gốc chỉ đi qua cổng gửi.
/// </summary>
internal sealed class PasswordResetCodeIssuer(
    IUserAccountGateway accounts,
    IOtpCodeStore otpStore,
    IOtpSender sender,
    IClock clock)
{
    public async Task IssueAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        if (!await accounts.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            return;
        }

        var now = clock.UtcNow.UtcDateTime;
        var rawOtp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var otp = OtpCode.Issue(normalizedEmail, OtpCode.ResetPasswordPurpose, TokenHasher.Hash(rawOtp), now);
        if (!await otpStore.TryIssueAsync(otp, OtpCode.MaxIssuedPerHour, now.AddHours(-1), cancellationToken))
        {
            return;
        }

        await sender.SendPasswordResetCodeAsync(normalizedEmail, rawOtp, otp.ExpiresAt, cancellationToken);
    }
}
