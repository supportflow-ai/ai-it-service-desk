using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Infrastructure;

/// <summary>
/// Đọc thành viên nhóm hỗ trợ trực tiếp từ PostgreSQL ở mỗi lần gọi (không cache) để thu hồi có hiệu lực ngay
/// ở tầng dữ liệu. Chỉ tính nhóm đang hoạt động. Không truy cập bảng Identity.
/// </summary>
internal sealed class PostgresSupportTeamMembershipReader(DbContext context) : ISupportTeamMembershipReader
{
    public async Task<IReadOnlyList<Guid>> GetTeamIdsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) return [];
        return await ActiveMembers(context)
            .Where(member => member.UserId == userId)
            .Select(member => member.TeamId)
            .OrderBy(teamId => teamId)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> IsMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default) =>
        teamId == Guid.Empty || userId == Guid.Empty
            ? Task.FromResult(false)
            : ActiveMembers(context).AnyAsync(member => member.TeamId == teamId && member.UserId == userId, cancellationToken);

    public Task<bool> IsLeadAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default) =>
        teamId == Guid.Empty || userId == Guid.Empty
            ? Task.FromResult(false)
            : ActiveMembers(context).AnyAsync(
                member => member.TeamId == teamId && member.UserId == userId && member.IsLead, cancellationToken);

    /// <summary>Dòng thành viên của nhóm đang hoạt động.</summary>
    internal static IQueryable<SupportTeamMember> ActiveMembers(DbContext context) =>
        context.Set<SupportTeamMember>().AsNoTracking()
            .Where(member => context.Set<SupportTeam>().Any(team => team.Id == member.TeamId && team.IsActive));
}
