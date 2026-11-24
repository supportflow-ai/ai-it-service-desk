using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.Ports;

/// <summary>
/// Cổng lưu trữ và quản lý nhóm hỗ trợ và thành viên nhóm.
/// </summary>
public interface ISupportTeamStore
{
    Task<IReadOnlyList<SupportTeam>> GetActiveTeamsAsync(CancellationToken cancellationToken = default);

    Task<SupportTeam?> GetTeamByIdAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task<bool> ExistsCodeAsync(string code, CancellationToken cancellationToken = default);

    Task AddTeamAsync(SupportTeam team, CancellationToken cancellationToken = default);

    Task UpdateTeamAsync(SupportTeam team, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupportTeamMember>> GetMembersAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task<bool> ExistsMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default);

    Task AddMemberAsync(SupportTeamMember member, CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default);
}
