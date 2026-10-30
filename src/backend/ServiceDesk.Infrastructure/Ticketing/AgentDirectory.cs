using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Infrastructure.Persistence;

namespace ServiceDesk.Infrastructure.Ticketing;

internal sealed class AgentDirectory(ApplicationDbContext context) : IAgentDirectory
{
    public async Task<bool> IsAgentAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        var agentRole = await context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Name == RoleNames.Agent, cancellationToken);
        if (agentRole is null) return false;
        var userIdStr = agentId.ToString();
        return await context.UserRoles.AsNoTracking().AnyAsync(ur => ur.UserId == userIdStr && ur.RoleId == agentRole.Id, cancellationToken);
    }

    public async Task<string> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userIdStr = userId.ToString();
        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userIdStr, cancellationToken);
        if (user is null) return "Người dùng";
        return !string.IsNullOrWhiteSpace(user.UserName) ? user.UserName : (!string.IsNullOrWhiteSpace(user.Email) ? user.Email : "Người dùng");
    }
}
