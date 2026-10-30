using ServiceDesk.Application.Common;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.Ticketing.ClaimTicket;

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
    ITicketTimelineStore timelineStore,
    ICurrentUser currentUser,
    IClock clock) : IClaimTicket
{
    public async Task<TicketDto> ExecuteAsync(ClaimTicketCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        var isStaff = currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin);
        if (!isStaff)
        {
            throw new UnauthorizedAccessException("Only an agent or admin can claim a ticket.");
        }

        ArgumentNullException.ThrowIfNull(command);

        var ticket = await writer.GetForUpdateAsync(command.Id, cancellationToken);
        if (ticket == null)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        if (command.ExpectedRevision.HasValue && command.ExpectedRevision.Value > 0)
        {
            var currentRev = writer.GetRevision(ticket);
            if (currentRev != 0 && currentRev != command.ExpectedRevision.Value)
            {
                throw new ConcurrencyConflictException("Dữ liệu đã bị thay đổi bởi người khác hoặc phiên làm việc đã cũ. Vui lòng tải lại trang.");
            }
        }

        var now = clock.UtcNow.UtcDateTime;
        try
        {
            ticket.Claim(actorId, now);
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message);
        }

        var updated = await writer.UpdateTicketAsync(ticket, cancellationToken);
        var audit = TicketComment.CreatePublic(ticket.Id, actorId, "Hệ thống", "System", "Kỹ thuật viên đã tiếp nhận xử lý yêu cầu.", now);
        await timelineStore.AddAsync(audit, cancellationToken);
        return updated;
    }
}
