using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Common;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
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
/// Xử lý tiếp nhận vé (khung chung của lệnh chuyển trạng thái: <see cref="TicketTransitions"/>):
/// - Yêu cầu người gọi có vai trò Agent hoặc Admin.
/// - Vé đã phân công cho người khác chỉ Admin mới nhận thay được.
/// - Chuyển Submitted/Assigned sang InProgress, gán AssigneeId và ghi log vào timeline.
/// </summary>
internal sealed class ClaimTicketUseCase(
    ITicketWriter writer,
    ICurrentUser currentUser,
    IClock clock) : IClaimTicket
{
    public async Task<TicketDto> ExecuteAsync(ClaimTicketCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await new TicketTransitions(writer, currentUser, clock).RunAsStaffAsync(
            command.Id, command.ExpectedRevision, "nhận xử lý phiếu",
            apply: (ticket, actorId, now) => ticket.Claim(actorId, now),
            auditMessage: "Kỹ thuật viên đã tiếp nhận xử lý yêu cầu.",
            cancellationToken: cancellationToken,
            beforeRevisionCheck: (ticket, actorId, _) =>
            {
                if (ticket.Status == TicketStatus.Assigned && ticket.AssignedToId != actorId && !TicketPermissions.IsAdmin(currentUser))
                {
                    throw new ForbiddenException("ticket_assigned_to_other", "Vé đã được phân công cho kỹ thuật viên khác.");
                }

                return Task.CompletedTask;
            });
    }
}
