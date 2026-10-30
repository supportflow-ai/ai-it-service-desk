using ServiceDesk.Application.Common;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.Ticketing.UnassignTicket;

/// <summary>Lệnh hủy phân công ticket.</summary>
public record UnassignTicketCommand(Guid Id, uint? ExpectedRevision = null);

/// <summary>Hợp đồng use case hủy phân công vé.</summary>
public interface IUnassignTicket
{
    Task<TicketDto> ExecuteAsync(UnassignTicketCommand command, CancellationToken cancellationToken = default);
}

internal sealed class UnassignTicketUseCase(
    ITicketWriter writer,
    ITicketTimelineStore timelineStore,
    ICurrentUser currentUser,
    IClock clock) : IUnassignTicket
{
    public async Task<TicketDto> ExecuteAsync(UnassignTicketCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        var isStaff = currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin);
        if (!isStaff)
        {
            throw new UnauthorizedAccessException("Only agents or admins can unassign tickets.");
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
            ticket.Unassign(now);
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message);
        }

        var updated = await writer.UpdateTicketAsync(ticket, cancellationToken);
        var audit = TicketComment.CreatePublic(ticket.Id, actorId, "Hệ thống", "System", "Yêu cầu đã được hủy phân công xử lý.", now);
        await timelineStore.AddAsync(audit, cancellationToken);
        return updated;
    }
}
