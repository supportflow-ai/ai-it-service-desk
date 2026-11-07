using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Identity.Application.Common;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Ports;

namespace ServiceDesk.Modules.Identity.Application.ResetPassword;

public interface IResetPassword
{
    Task<IdentityOperationResult> ExecuteAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Đặt lại mật khẩu bằng OTP. Toàn bộ kiểm tra mã, đếm lần nhập sai, đổi mật khẩu, đánh dấu mã đã dùng
/// và thu hồi refresh token nằm trong một giao dịch của <see cref="IPasswordResetGateway"/>.
/// </summary>
internal sealed class ResetPasswordUseCase(
    IPasswordResetGateway passwordReset,
    IClock clock) : IResetPassword
{
    public Task<IdentityOperationResult> ExecuteAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.OtpCode) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Task.FromResult(IdentityOperationResult.Failed("Email, OTP code and new password are required."));
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var codeHash = TokenHasher.Hash(request.OtpCode.Trim());
        return passwordReset.ResetWithOtpAsync(normalizedEmail, codeHash, request.NewPassword, clock.UtcNow.UtcDateTime, cancellationToken);
    }
}
