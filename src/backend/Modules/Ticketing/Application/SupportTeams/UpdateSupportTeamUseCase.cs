using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Ticketing.Application.SupportTeams;

public interface IUpdateSupportTeam
{
    Task<SupportTeamDto> ExecuteAsync(Guid teamId, UpdateSupportTeamRequest request, CancellationToken cancellationToken = default);
}

internal sealed class UpdateSupportTeamUseCase(
    ISupportTeamStore supportTeamStore,
    IAgentDirectory agentDirectory,
    ICurrentUser currentUser) : IUpdateSupportTeam
{
    public async Task<SupportTeamDto> ExecuteAsync(Guid teamId, UpdateSupportTeamRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!currentUser.IsInRole(RoleNames.Admin))
        {
            throw new UnauthorizedAccessException("Chỉ quản trị viên mới có quyền cập nhật nhóm hỗ trợ.");
        }

        var team = await supportTeamStore.GetTeamByIdAsync(teamId, cancellationToken);
        if (team == null)
        {
            throw new KeyNotFoundException("Không tìm thấy nhóm hỗ trợ.");
        }

        team.Update(request.Name, request.IsActive);
        await supportTeamStore.UpdateTeamAsync(team, cancellationToken);

        var members = await supportTeamStore.GetMembersAsync(teamId, cancellationToken);
        var memberDtos = new List<SupportTeamMemberDto>(members.Count);
        foreach (var m in members)
        {
            var name = await agentDirectory.GetDisplayNameAsync(m.UserId, cancellationToken);
            memberDtos.Add(new SupportTeamMemberDto(m.UserId, name, m.IsLead));
        }

        return new SupportTeamDto(
            team.Id,
            team.Code,
            team.Name,
            team.IsTriageQueue,
            team.IsActive,
            memberDtos);
    }
}
