using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.UnassignTicket;

/// <summary>Lệnh hủy phân công ticket.</summary>
public record UnassignTicketCommand(Guid Id, uint? ExpectedRevision = null);

/// <summary>Hợp đồng use case hủy phân công vé.</summary>
public interface IUnassignTicket
{
    Task<TicketDto> ExecuteAsync(UnassignTicketCommand command, CancellationToken cancellationToken = default);
}

internal sealed class UnassignTicketUseCase(
    ITicketWriter writer,
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

        if (!command.ExpectedRevision.HasValue || command.ExpectedRevision.Value == 0)
        {
            throw new ArgumentException("Expected revision is required.");
        }

        var currentRev = writer.GetRevision(ticket);
        if (currentRev != command.ExpectedRevision.Value)
        {
            throw new ConcurrencyConflictException("Dữ liệu đã bị thay đổi bởi người khác hoặc phiên làm việc đã cũ. Vui lòng tải lại trang.");
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

        var audit = TicketComment.CreatePublic(ticket.Id, actorId, "Hệ thống", "System", "Yêu cầu đã được hủy phân công xử lý.", now);
        return await writer.UpdateTicketWithAuditAsync(ticket, audit, cancellationToken);
    }
}
