using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Ticketing.Infrastructure;

internal sealed class AgentDirectory(DbContext context) : IAgentDirectory
{
    public async Task<bool> IsAgentAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        var staffRoleIds = await context.Set<IdentityRole>().AsNoTracking()
            .Where(r => r.Name == RoleNames.Agent || r.Name == RoleNames.Admin)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
        if (staffRoleIds.Count == 0) return false;
        var userIdStr = agentId.ToString();
        return await context.Set<IdentityUserRole<string>>().AsNoTracking()
            .AnyAsync(ur => ur.UserId == userIdStr && staffRoleIds.Contains(ur.RoleId), cancellationToken);
    }

    public async Task<string> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userIdStr = userId.ToString();
        var user = await context.Set<IdentityUser>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == userIdStr, cancellationToken);
        if (user is null) return "Người dùng";
        var name = await context.Set<IdentityUserClaim<string>>().AsNoTracking()
            .Where(c => c.UserId == userIdStr && c.ClaimType == ClaimTypes.Name)
            .Select(c => c.ClaimValue)
            .FirstOrDefaultAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(name) ? name : "Người dùng";
    }
}
