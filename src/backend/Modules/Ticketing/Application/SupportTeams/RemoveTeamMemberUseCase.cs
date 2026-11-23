using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.Ticketing.Application.SupportTeams;

public record RemoveTeamMemberCommand(Guid TeamId, Guid UserId);

public interface IRemoveTeamMember
{
    Task ExecuteAsync(RemoveTeamMemberCommand command, CancellationToken cancellationToken = default);
}

internal sealed class RemoveTeamMemberUseCase(
    ISupportTeamStore store,
    ISupportTeamMembershipReader membershipReader,
    ICurrentUser currentUser) : IRemoveTeamMember
{
    public async Task ExecuteAsync(RemoveTeamMemberCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actorId = TicketActor.RequireId(currentUser);
        var isAdmin = TicketPermissions.IsAdmin(currentUser);
        var isLead = await membershipReader.IsLeadAsync(command.TeamId, actorId, cancellationToken);

        if (!isAdmin && !isLead)
        {
            throw new UnauthorizedAccessException("Chỉ Quản trị viên (Admin) hoặc Trưởng nhóm (Team Lead) mới có quyền xóa thành viên khỏi nhóm.");
        }

        var team = await store.GetTeamByIdAsync(command.TeamId, cancellationToken);
        if (team is null)
        {
            throw new KeyNotFoundException("Nhóm hỗ trợ không tồn tại.");
        }

        var exists = await store.ExistsMemberAsync(command.TeamId, command.UserId, cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException("Thành viên không tồn tại trong nhóm hỗ trợ này.");
        }

        await store.RemoveMemberAsync(command.TeamId, command.UserId, cancellationToken);
    }
}
