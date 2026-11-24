using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Common;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Ticketing.Application.TransferTicket;

internal sealed class TransferTicketUseCase(
    ITicketWriter writer,
    ISupportTeamStore supportTeamStore,
    ISupportTeamMembershipReader membershipReader,
    ICurrentUser currentUser,
    IClock clock) : ITransferTicket
{
    public async Task<TicketDto> ExecuteAsync(TransferTicketCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Request);

        var isStaff = TicketPermissions.IsStaff(currentUser);
        if (!isStaff)
        {
            throw new UnauthorizedAccessException("Chỉ kỹ thuật viên hoặc quản trị viên mới có thể điều phối chuyển nhóm.");
        }

        var scope = TicketPermissions.ReadScopeFor(currentUser, actorId);
        var ticket = await writer.GetForUpdateAsync(command.Id, scope, cancellationToken);
        if (ticket == null)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        var isAdmin = TicketPermissions.IsAdmin(currentUser);
        var isTeamLead = ticket.SupportTeamId.HasValue &&
            await membershipReader.IsLeadAsync(ticket.SupportTeamId.Value, actorId, cancellationToken);

        if (!isAdmin && !isTeamLead)
        {
            throw new UnauthorizedAccessException("Chỉ Team Lead của nhóm phụ trách phiếu hoặc Admin mới có quyền điều phối chuyển đội.");
        }

        var targetTeam = await supportTeamStore.GetTeamByIdAsync(command.Request.TargetTeamId, cancellationToken);
        if (targetTeam == null || !targetTeam.IsActive)
        {
            throw new ArgumentException("Nhóm hỗ trợ đích không tồn tại hoặc đã ngừng hoạt động.");
        }

        var targetTeamHasCurrentAssignee = false;
        if (ticket.AssignedToId.HasValue)
        {
            targetTeamHasCurrentAssignee = await membershipReader.IsMemberAsync(command.Request.TargetTeamId, ticket.AssignedToId.Value, cancellationToken);
        }

        TicketCommandGuard.RequireCurrentRevision(writer, ticket, command.Request.ExpectedRevision);

        var now = clock.UtcNow.UtcDateTime;
        TicketCommandGuard.ApplyRule(ticket, t => t.TransferToTeam(command.Request.TargetTeamId, command.Request.Reason, targetTeamHasCurrentAssignee, now));

        var auditText = $"Điều phối chuyển sang nhóm hỗ trợ [{targetTeam.Name}]. Lý do: {command.Request.Reason.Trim()}";
        return await TicketCommandGuard.SaveWithSystemAuditAsync(writer, ticket, actorId, auditText, now, cancellationToken);
    }
}
