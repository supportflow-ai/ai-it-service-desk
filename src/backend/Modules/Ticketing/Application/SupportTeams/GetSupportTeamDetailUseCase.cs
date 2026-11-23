namespace ServiceDesk.Modules.Ticketing.Application.SupportTeams;

using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Application.Abstractions;

public record GetSupportTeamDetailQuery(Guid TeamId);

public interface IGetSupportTeamDetail
{
    Task<SupportTeamDto?> ExecuteAsync(GetSupportTeamDetailQuery query, CancellationToken cancellationToken = default);
}

internal sealed class GetSupportTeamDetailUseCase(
    ISupportTeamStore store,
    IUserDirectory userDirectory,
    ICurrentUser currentUser) : IGetSupportTeamDetail
{
    public async Task<SupportTeamDto?> ExecuteAsync(GetSupportTeamDetailQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!TicketPermissions.IsStaff(currentUser))
        {
            throw new UnauthorizedAccessException("Chỉ nhân viên hỗ trợ (Agent hoặc Admin) mới có quyền xem thông tin nhóm hỗ trợ.");
        }

        var team = await store.GetTeamByIdAsync(query.TeamId, cancellationToken);
        if (team == null) return null;

        var members = await store.GetMembersAsync(team.Id, cancellationToken);
        var memberDtos = new List<SupportTeamMemberDto>(members.Count);

        foreach (var member in members)
        {
            var displayName = await userDirectory.FindDisplayNameAsync(member.UserId, cancellationToken);
            memberDtos.Add(new SupportTeamMemberDto(member.UserId, displayName, member.IsLead));
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
