using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Identity.Ports;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Identity.AssignUserRoles;

public interface IAssignUserRoles
{
    Task<IdentityOperationResult> ExecuteAsync(Guid targetUserId, AssignRolesRequest request, CancellationToken cancellationToken = default);
}

internal sealed class AssignUserRolesUseCase(
    IUserAccountGateway accounts,
    ICurrentUser currentUser) : IAssignUserRoles
{
    public Task<IdentityOperationResult> ExecuteAsync(Guid targetUserId, AssignRolesRequest request, CancellationToken cancellationToken = default)
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
            return Task.FromResult(IdentityOperationResult.Failed("At least one role must be assigned."));
        }

        foreach (var r in request.Roles)
        {
            if (!RoleNames.All.Contains(r, StringComparer.OrdinalIgnoreCase))
            {
                return Task.FromResult(IdentityOperationResult.Failed($"Invalid role: {r}"));
            }
        }

        return accounts.AssignRolesAsync(targetUserId.ToString(), request.Roles, cancellationToken);
    }
}
