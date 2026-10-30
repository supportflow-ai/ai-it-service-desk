using ServiceDesk.Application.Common;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.Ticketing.ResolveTicket;

/// <summary>Lệnh hoàn thành xử lý ticket (Resolve).</summary>
public record ResolveTicketCommand(Guid Id, uint? ExpectedRevision = null);

/// <summary>Hợp đồng use case đánh dấu giải quyết vé.</summary>
public interface IResolveTicket
{
    Task<TicketDto> ExecuteAsync(ResolveTicketCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý đánh dấu hoàn thành vé:
/// - Yêu cầu người gọi có vai trò Agent hoặc Admin.
/// - Trạng thái vé bắt buộc phải là InProgress.
/// - Chuyển trạng thái sang Resolved và ghi log vào timeline.
/// </summary>
internal sealed class ResolveTicketUseCase(
    ITicketWriter writer,
    ITicketTimelineStore timelineStore,
    ICurrentUser currentUser,
    IClock clock) : IResolveTicket
{
    public async Task<TicketDto> ExecuteAsync(ResolveTicketCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(command);

        var ticket = await writer.GetForUpdateAsync(command.Id, cancellationToken);
        if (ticket == null)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        var isStaff = currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin);
        if (!isStaff)
        {
            throw new UnauthorizedAccessException("Only an agent or admin can resolve this ticket.");
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
            ticket.Resolve(now);
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message);
        }

        var updated = await writer.UpdateTicketAsync(ticket, cancellationToken);
        var audit = TicketComment.CreatePublic(ticket.Id, actorId, "Hệ thống", "System", "Yêu cầu đã được đánh dấu giải quyết xong.", now);
        await timelineStore.AddAsync(audit, cancellationToken);
        return updated;
    }
}
