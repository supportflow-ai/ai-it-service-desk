using ServiceDesk.Modules.Identity.Application.Contracts;

namespace ServiceDesk.Modules.Identity.Application.Ports;

/// <summary>
/// Đặt lại mật khẩu bằng OTP trong một giao dịch: khóa mã còn hiệu lực mới nhất, đếm lần nhập sai,
/// đổi mật khẩu, đánh dấu mã đã dùng và thu hồi mọi refresh token của người dùng.
/// </summary>
public interface IPasswordResetGateway
{
    Task<IdentityOperationResult> ResetWithOtpAsync(string normalizedEmail, string codeHash, string newPassword, DateTime now,
        CancellationToken cancellationToken = default);
}
