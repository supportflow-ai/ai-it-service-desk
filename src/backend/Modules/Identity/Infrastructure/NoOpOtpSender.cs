using Microsoft.Extensions.Logging;
using ServiceDesk.Modules.Identity.Application.Ports;

namespace ServiceDesk.Modules.Identity.Infrastructure;

/// <summary>
/// Cổng gửi OTP mặc định khi chưa chọn nhà cung cấp email: không gửi mã và chỉ ghi cảnh báo
/// (không ghi mã, không ghi email). Thay bằng adapter gửi thật khi nhóm chốt kênh gửi.
/// </summary>
internal sealed class NoOpOtpSender(ILogger<NoOpOtpSender> logger) : IOtpSender
{
    public Task SendPasswordResetCodeAsync(string email, string code, DateTime expiresAt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogWarning("Chưa cấu hình kênh gửi OTP; mã đặt lại mật khẩu không được gửi.");
        return Task.CompletedTask;
    }
}
