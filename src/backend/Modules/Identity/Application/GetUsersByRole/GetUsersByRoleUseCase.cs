using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Identity.Application.GetUsersByRole;

public record GetUsersByRoleQuery(string Role);

public sealed record UserDirectoryEntry(string UserId, string? FullName);

public interface IGetUsersByRole
{
    Task<IReadOnlyList<UserDirectoryEntry>> ExecuteAsync(GetUsersByRoleQuery query, CancellationToken cancellationToken = default);
}

internal sealed class GetUsersByRoleUseCase(IUserAccountGateway userGateway, ICurrentUser currentUser) : IGetUsersByRole
{
    public async Task<IReadOnlyList<UserDirectoryEntry>> ExecuteAsync(GetUsersByRoleQuery query, CancellationToken cancellationToken = default)
    {
        var isStaff = currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin);
        if (!isStaff)
        {
            throw new UnauthorizedAccessException("Only agents or admins can query user lists.");
        }

        ArgumentNullException.ThrowIfNull(query);
        var role = query.Role.Equals(RoleNames.Agent, StringComparison.OrdinalIgnoreCase) ? RoleNames.Agent
            : query.Role.Equals(RoleNames.Admin, StringComparison.OrdinalIgnoreCase) ? RoleNames.Admin
            : throw new ArgumentException("Only agents or admins can be queried.");

        var users = await userGateway.GetUsersInRoleAsync(role, cancellationToken);
        return users.Select(user => new UserDirectoryEntry(user.UserId, user.FullName)).ToArray();
    }
}
