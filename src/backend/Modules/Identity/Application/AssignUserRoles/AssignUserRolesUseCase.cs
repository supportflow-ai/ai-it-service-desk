using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Application;
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
/// và đổi security stamp để access token cũ bị từ chối ở lần xác thực kế tiếp.
/// Không cho gỡ vai trò Admin của quản trị viên cuối cùng (kiểm tra trong gateway).
/// </summary>
internal sealed class AssignUserRolesUseCase(
    IUserAccountGateway accounts,
    ICurrentUser currentUser,
    IClock clock) : IAssignUserRoles
{
    public async Task<IdentityOperationResult> ExecuteAsync(Guid targetUserId, AssignRolesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = AuthenticatedActor.RequireId(currentUser);
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

        // Gateway đổi vai trò và thu hồi refresh token trong cùng một giao dịch.
        return await accounts.AssignRolesAsync(targetUserId.ToString(), request.Roles, clock.UtcNow.UtcDateTime, cancellationToken);
    }
}
