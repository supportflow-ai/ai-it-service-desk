using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Ticketing.Application.SupportTeams;

public record AddTeamMemberCommand(Guid TeamId, AddTeamMemberRequest Request);

public interface IAddTeamMember
{
    Task<SupportTeamMemberDto> ExecuteAsync(AddTeamMemberCommand command, CancellationToken cancellationToken = default);
}

internal sealed class AddTeamMemberUseCase(
    ISupportTeamStore store,
    ISupportTeamMembershipReader membershipReader,
    IUserDirectory userDirectory,
    ICurrentUser currentUser) : IAddTeamMember
{
    public async Task<SupportTeamMemberDto> ExecuteAsync(AddTeamMemberCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Request);

        var actorId = TicketActor.RequireId(currentUser);
        var isAdmin = TicketPermissions.IsAdmin(currentUser);
        var isLead = await membershipReader.IsLeadAsync(command.TeamId, actorId, cancellationToken);

        if (!isAdmin && !isLead)
        {
            throw new UnauthorizedAccessException("Chỉ Quản trị viên (Admin) hoặc Trưởng nhóm (Team Lead) mới có quyền thêm thành viên vào nhóm.");
        }

        var team = await store.GetTeamByIdAsync(command.TeamId, cancellationToken);
        if (team is null || !team.IsActive)
        {
            throw new KeyNotFoundException("Nhóm hỗ trợ không tồn tại hoặc đã ngừng hoạt động.");
        }

        var targetUserId = command.Request.UserId;
        if (targetUserId == Guid.Empty)
        {
            throw new ArgumentException("UserId không được để trống.", nameof(command));
        }

        var isStaff = await userDirectory.IsInAnyRoleAsync(targetUserId, [RoleNames.Agent, RoleNames.Admin], cancellationToken);
        if (!isStaff)
        {
            throw new ArgumentException("Chỉ người dùng có vai trò Agent hoặc Admin mới có thể tham gia nhóm hỗ trợ.");
        }

        var alreadyMember = await store.ExistsMemberAsync(command.TeamId, targetUserId, cancellationToken);
        if (alreadyMember)
        {
            throw new TeamMemberConflictException("Người dùng đã là thành viên của nhóm hỗ trợ này.");
        }

        var member = SupportTeamMember.Create(command.TeamId, targetUserId, command.Request.IsLead);
        await store.AddMemberAsync(member, cancellationToken);

        var displayName = await userDirectory.FindDisplayNameAsync(targetUserId, cancellationToken);
        return new SupportTeamMemberDto(targetUserId, displayName, member.IsLead);
    }
}
