using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.Ticketing.Application.SupportTeams;

public interface IGetSupportTeams
{
    Task<IReadOnlyList<SupportTeamDto>> ExecuteAsync(CancellationToken cancellationToken = default);
}

internal sealed class GetSupportTeamsUseCase(
    ISupportTeamStore store,
    IUserDirectory userDirectory,
    ICurrentUser currentUser) : IGetSupportTeams
{
    public async Task<IReadOnlyList<SupportTeamDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!TicketPermissions.IsStaff(currentUser))
        {
            throw new UnauthorizedAccessException("Chỉ nhân viên hỗ trợ (Agent hoặc Admin) mới có quyền xem danh sách nhóm hỗ trợ.");
        }

        var teams = await store.GetActiveTeamsAsync(cancellationToken);
        var result = new List<SupportTeamDto>(teams.Count);

        foreach (var team in teams)
        {
            var members = await store.GetMembersAsync(team.Id, cancellationToken);
            var memberDtos = new List<SupportTeamMemberDto>(members.Count);

            foreach (var member in members)
            {
                var displayName = await userDirectory.FindDisplayNameAsync(member.UserId, cancellationToken);
                memberDtos.Add(new SupportTeamMemberDto(member.UserId, displayName, member.IsLead));
            }

            result.Add(new SupportTeamDto(
                team.Id,
                team.Code,
                team.Name,
                team.IsTriageQueue,
                team.IsActive,
                memberDtos));
        }

        return result;
    }
}
