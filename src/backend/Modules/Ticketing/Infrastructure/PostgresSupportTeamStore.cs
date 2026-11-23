using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Infrastructure;

internal sealed class PostgresSupportTeamStore(DbContext context) : ISupportTeamStore
{
    public async Task<IReadOnlyList<SupportTeam>> GetActiveTeamsAsync(CancellationToken cancellationToken = default) =>
        await context.Set<SupportTeam>()
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

    public async Task<SupportTeam?> GetTeamByIdAsync(Guid teamId, CancellationToken cancellationToken = default) =>
        await context.Set<SupportTeam>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);

    public async Task<IReadOnlyList<SupportTeamMember>> GetMembersAsync(Guid teamId, CancellationToken cancellationToken = default) =>
        await context.Set<SupportTeamMember>()
            .AsNoTracking()
            .Where(m => m.TeamId == teamId)
            .ToListAsync(cancellationToken);

    public async Task<bool> ExistsMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default) =>
        await context.Set<SupportTeamMember>()
            .AnyAsync(m => m.TeamId == teamId && m.UserId == userId, cancellationToken);

    public async Task AddMemberAsync(SupportTeamMember member, CancellationToken cancellationToken = default)
    {
        context.Set<SupportTeamMember>().Add(member);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default)
    {
        var member = await context.Set<SupportTeamMember>()
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId, cancellationToken);

        if (member is not null)
        {
            context.Set<SupportTeamMember>().Remove(member);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
