using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Infrastructure.Persistence;

namespace ServiceDesk.Infrastructure.Ticketing;

internal sealed class AgentDirectory(ApplicationDbContext context) : IAgentDirectory
{
    public async Task<bool> IsAgentAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        var staffRoleIds = await context.Roles.AsNoTracking()
            .Where(r => r.Name == RoleNames.Agent || r.Name == RoleNames.Admin)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
        if (staffRoleIds.Count == 0) return false;
        var userIdStr = agentId.ToString();
        return await context.UserRoles.AsNoTracking()
            .AnyAsync(ur => ur.UserId == userIdStr && staffRoleIds.Contains(ur.RoleId), cancellationToken);
    }

    public async Task<string> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userIdStr = userId.ToString();
        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userIdStr, cancellationToken);
        if (user is null) return "Người dùng";
        return !string.IsNullOrWhiteSpace(user.UserName) ? user.UserName : (!string.IsNullOrWhiteSpace(user.Email) ? user.Email : "Người dùng");
    }
}
