using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Common;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.Ticketing.Application.ResumeTicket;

/// <summary>
/// Xử lý tiếp tục xử lý vé (PendingUser hoặc PendingExternal -> InProgress):
/// - Yêu cầu người gọi là kỹ thuật viên được phân công, Trưởng nhóm phụ trách (active team), hoặc Admin.
/// - Trạng thái vé bắt buộc phải là PendingUser hoặc PendingExternal.
/// - Tiếp tục đồng hồ Resolution SLA và ghi log hệ thống.
/// </summary>
internal sealed class ResumeTicketUseCase(
    ITicketWriter writer,
    ISupportTeamMembershipReader membershipReader,
    ICurrentUser currentUser,
    IClock clock) : IResumeTicket
{
    public async Task<TicketDto> ExecuteAsync(ResumeTicketCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(command);

        var isStaff = TicketPermissions.IsStaff(currentUser);
        if (!isStaff)
        {
            throw new UnauthorizedAccessException("Chỉ kỹ thuật viên hoặc Quản trị viên mới có quyền tiếp tục xử lý phiếu.");
        }

        var scope = TicketPermissions.ReadScopeFor(currentUser, actorId);
        var ticket = await writer.GetForUpdateAsync(command.Id, scope, cancellationToken);
        if (ticket == null)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        var isAdmin = TicketPermissions.IsAdmin(currentUser);
        var isAssignedAgent = ticket.AssignedToId.HasValue && ticket.AssignedToId.Value == actorId;
        var isTeamLead = ticket.SupportTeamId.HasValue &&
            await membershipReader.IsLeadAsync(ticket.SupportTeamId.Value, actorId, cancellationToken);

        if (!isAdmin && !isAssignedAgent && !isTeamLead)
        {
            throw new UnauthorizedAccessException("Chỉ kỹ thuật viên được phân công, Trưởng nhóm phụ trách hoặc Quản trị viên mới có quyền tiếp tục xử lý phiếu.");
        }

        TicketCommandGuard.RequireCurrentRevision(writer, ticket, command.ExpectedRevision);

        var now = clock.UtcNow.UtcDateTime;
        TicketCommandGuard.ApplyRule(ticket, t => t.ResumeFromPending(now), includeArgumentErrors: true);

        return await TicketCommandGuard.SaveWithSystemAuditAsync(writer, ticket, actorId, "Yêu cầu đã được tiếp tục xử lý.", now, cancellationToken);
    }
}
