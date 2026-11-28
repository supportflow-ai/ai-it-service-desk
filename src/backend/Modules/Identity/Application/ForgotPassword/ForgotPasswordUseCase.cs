using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.SharedKernel.Application;

namespace ServiceDesk.Modules.Identity.Application.ForgotPassword;

public interface IForgotPassword
{
    Task<IdentityOperationResult> ExecuteAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xác nhận tiếp nhận sau khi lưu yêu cầu bền vững; lỗi đầu vào/quá tải không được báo thành công.
/// Kiểm tài khoản và gửi mã chạy nền nên phản hồi tiếp nhận không phụ thuộc email có tồn tại.
/// </summary>
internal sealed class ForgotPasswordUseCase(IPasswordResetQueue queue) : IForgotPassword
{
    public async Task<IdentityOperationResult> ExecuteAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return IdentityOperationResult.Failed("Email is required.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!await queue.EnqueueAsync(request.Email.Trim().ToLowerInvariant(), cancellationToken))
            throw new ServiceUnavailableException("Hàng đợi đặt lại mật khẩu tạm thời quá tải. Vui lòng thử lại sau.");
        return IdentityOperationResult.Success();
    }
}
