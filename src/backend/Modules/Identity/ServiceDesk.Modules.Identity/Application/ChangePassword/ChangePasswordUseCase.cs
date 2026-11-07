using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Ports;

namespace ServiceDesk.Modules.Identity.Application.ChangePassword;

public interface IChangePassword
{
    Task<IdentityOperationResult> ExecuteAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Đổi mật khẩu của người đang đăng nhập; thành công thì thu hồi mọi refresh token để đăng xuất các phiên khác.</summary>
internal sealed class ChangePasswordUseCase(
    IUserAccountGateway accounts,
    ICurrentUser currentUser,
    IClock clock) : IChangePassword
{
    public async Task<IdentityOperationResult> ExecuteAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actorId = AuthenticatedActor.RequireId(currentUser);
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return IdentityOperationResult.Failed("Current password and new password are required.");
        }

        // Gateway đổi mật khẩu và thu hồi refresh token trong cùng một giao dịch.
        return await accounts.ChangePasswordAsync(actorId.ToString(), request.CurrentPassword, request.NewPassword,
            clock.UtcNow.UtcDateTime, cancellationToken);
    }
}
