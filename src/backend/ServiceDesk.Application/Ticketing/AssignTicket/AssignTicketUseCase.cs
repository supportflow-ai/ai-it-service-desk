using ServiceDesk.Application.Common;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.Ticketing.AssignTicket;

/// <summary>Lệnh phân công ticket cho kỹ thuật viên.</summary>
public record AssignTicketCommand(Guid Id, Guid AgentId, uint? ExpectedRevision = null);

/// <summary>Hợp đồng use case phân công vé.</summary>
public interface IAssignTicket
{
    Task<TicketDto> ExecuteAsync(AssignTicketCommand command, CancellationToken cancellationToken = default);
}

internal sealed class AssignTicketUseCase(
    ITicketWriter writer,
    ITicketTimelineStore timelineStore,
    IAgentDirectory agentDirectory,
    ICurrentUser currentUser,
    IClock clock) : IAssignTicket
{
    public async Task<TicketDto> ExecuteAsync(AssignTicketCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        var isStaff = currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin);
        if (!isStaff)
        {
            throw new UnauthorizedAccessException("Only agents or admins can assign tickets.");
        }

        ArgumentNullException.ThrowIfNull(command);

        if (!await agentDirectory.IsAgentAsync(command.AgentId, cancellationToken))
        {
            throw new ArgumentException("Kỹ thuật viên không tồn tại hoặc không có vai trò Agent.");
        }

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
            ticket.Assign(command.AgentId, now);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            throw new ArgumentException(ex.Message);
        }

        var updated = await writer.UpdateTicketAsync(ticket, cancellationToken);
        var audit = TicketComment.CreatePublic(ticket.Id, actorId, "Hệ thống", "System", "Yêu cầu đã được phân công cho kỹ thuật viên.", now);
        await timelineStore.AddAsync(audit, cancellationToken);
        return updated;
    }
}
