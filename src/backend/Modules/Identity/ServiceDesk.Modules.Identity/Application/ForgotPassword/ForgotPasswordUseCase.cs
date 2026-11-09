using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Ports;

namespace ServiceDesk.Modules.Identity.Application.ForgotPassword;

public interface IForgotPassword
{
    Task<IdentityOperationResult> ExecuteAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Nhận yêu cầu quên mật khẩu: chuẩn hóa email, đưa vào hàng đợi và luôn trả thành công. Việc kiểm email, phát và gửi mã
/// do <see cref="PasswordResetCodeIssuer"/> chạy nền, nên phản hồi giống nhau cả về nội dung lẫn thời gian dù email
/// có tồn tại hay không.
/// </summary>
internal sealed class ForgotPasswordUseCase(IPasswordResetQueue queue) : IForgotPassword
{
    public Task<IdentityOperationResult> ExecuteAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Task.FromResult(IdentityOperationResult.Failed("Email is required."));
        }

        cancellationToken.ThrowIfCancellationRequested();
        queue.TryEnqueue(request.Email.Trim().ToLowerInvariant());
        return Task.FromResult(IdentityOperationResult.Success());
    }
}
