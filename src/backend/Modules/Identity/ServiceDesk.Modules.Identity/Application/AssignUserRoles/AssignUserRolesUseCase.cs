using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Identity.Application.AssignUserRoles;

public interface IAssignUserRoles
{
    Task<IdentityOperationResult> ExecuteAsync(Guid targetUserId, AssignRolesRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Admin thay toàn bộ vai trò của một người dùng. Thành công thì thu hồi mọi refresh token của người đó
/// để phiên mới nhận vai trò mới; access token đang có vẫn hiệu lực tới khi hết hạn.
/// Không cho gỡ vai trò Admin của quản trị viên cuối cùng (kiểm tra trong gateway).
/// </summary>
internal sealed class AssignUserRolesUseCase(
    IUserAccountGateway accounts,
    IRefreshTokenStore refreshTokens,
    ICurrentUser currentUser,
    IClock clock) : IAssignUserRoles
{
    public async Task<IdentityOperationResult> ExecuteAsync(Guid targetUserId, AssignRolesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (targetUserId == Guid.Empty)
        {
            throw new ArgumentException("Target user ID is required.");
        }

        if (!currentUser.IsInRole(RoleNames.Admin))
        {
            throw new UnauthorizedAccessException("Chỉ quản trị viên mới có quyền phân quyền người dùng.");
        }

        if (request.Roles is null || request.Roles.Count == 0)
        {
            return IdentityOperationResult.Failed("At least one role must be assigned.");
        }

        foreach (var role in request.Roles)
        {
            if (!RoleNames.All.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                return IdentityOperationResult.Failed($"Invalid role: {role}");
            }
        }

        var result = await accounts.AssignRolesAsync(targetUserId.ToString(), request.Roles, cancellationToken);
        if (result.Succeeded)
        {
            await refreshTokens.RevokeAllForUserAsync(targetUserId.ToString(), clock.UtcNow.UtcDateTime, cancellationToken);
        }

        return result;
    }
}
