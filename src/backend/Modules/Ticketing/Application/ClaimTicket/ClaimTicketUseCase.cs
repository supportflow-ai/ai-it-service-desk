using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Common;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.ClaimTicket;

/// <summary>Lệnh yêu cầu tiếp nhận ticket (Claim).</summary>
public record ClaimTicketCommand(Guid Id, uint? ExpectedRevision = null);

/// <summary>Hợp đồng use case kỹ thuật viên nhận vé.</summary>
public interface IClaimTicket
{
    Task<TicketDto> ExecuteAsync(ClaimTicketCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý tiếp nhận vé:
/// - Yêu cầu người gọi có vai trò Agent hoặc Admin.
/// - Kiểm tra trạng thái vé phải là Submitted hoặc Assigned.
/// - Chuyển trạng thái sang InProgress, gán AssigneeId và ghi log vào timeline.
/// </summary>
internal sealed class ClaimTicketUseCase(
    ITicketWriter writer,
    ICurrentUser currentUser,
    IClock clock) : IClaimTicket
{
    public async Task<TicketDto> ExecuteAsync(ClaimTicketCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        var isStaff = TicketPermissions.IsStaff(currentUser);
        if (!isStaff)
        {
            throw TicketErrors.StaffRequired("nhận xử lý phiếu");
        }

        ArgumentNullException.ThrowIfNull(command);

        var scope = TicketPermissions.ReadScopeFor(currentUser, actorId);
        var ticket = await writer.GetForUpdateAsync(command.Id, scope, cancellationToken);
        if (ticket == null)
        {
            throw TicketErrors.NotFound();
        }

        if (ticket.Status == TicketStatus.Assigned && ticket.AssignedToId != actorId && !TicketPermissions.IsAdmin(currentUser))
        {
            throw new ForbiddenException("ticket_assigned_to_other", "Vé đã được phân công cho kỹ thuật viên khác.");
        }

        TicketCommandGuard.RequireCurrentRevision(writer, ticket, command.ExpectedRevision);

        var now = clock.UtcNow.UtcDateTime;
        ticket.Claim(actorId, now);

        return await TicketCommandGuard.SaveWithSystemAuditAsync(writer, ticket, actorId, "Kỹ thuật viên đã tiếp nhận xử lý yêu cầu.", now, cancellationToken);
    }
}
