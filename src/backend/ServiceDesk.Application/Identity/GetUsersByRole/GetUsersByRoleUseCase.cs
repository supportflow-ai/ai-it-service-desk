using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Identity.Ports;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Identity.GetUsersByRole;

public record GetUsersByRoleQuery(string Role);

public interface IGetUsersByRole
{
    Task<IReadOnlyList<UserInfo>> ExecuteAsync(GetUsersByRoleQuery query, CancellationToken cancellationToken = default);
}

internal sealed class GetUsersByRoleUseCase(IUserAccountGateway userGateway, ICurrentUser currentUser) : IGetUsersByRole
{
    public async Task<IReadOnlyList<UserInfo>> ExecuteAsync(GetUsersByRoleQuery query, CancellationToken cancellationToken = default)
    {
        var isStaff = currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin);
        if (!isStaff)
        {
            throw new UnauthorizedAccessException("Only agents or admins can query user lists.");
        }

        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrWhiteSpace(query.Role))
        {
            throw new ArgumentException("Role is required.");
        }

        return await userGateway.GetUsersInRoleAsync(query.Role, cancellationToken);
    }
}
