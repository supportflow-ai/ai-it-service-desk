using ServiceDesk.Application.Common;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Identity.Ports;

namespace ServiceDesk.Application.Identity.ChangePassword;

public interface IChangePassword
{
    Task<IdentityOperationResult> ExecuteAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Đổi mật khẩu của người đang đăng nhập; thành công thì thu hồi mọi refresh token để đăng xuất các phiên khác.</summary>
internal sealed class ChangePasswordUseCase(
    IUserAccountGateway accounts,
    IRefreshTokenStore refreshTokens,
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

        var result = await accounts.ChangePasswordAsync(actorId.ToString(), request.CurrentPassword, request.NewPassword, cancellationToken);
        if (result.Succeeded)
        {
            await refreshTokens.RevokeAllForUserAsync(actorId.ToString(), clock.UtcNow.UtcDateTime, cancellationToken);
        }

        return result;
    }
}
