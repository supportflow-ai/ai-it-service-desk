using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Common;
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
        var isStaff = TicketPermissions.IsStaff(currentUser);
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

        TicketCommandGuard.RequireCurrentRevision(writer, ticket, command.ExpectedRevision);

        var now = clock.UtcNow.UtcDateTime;
        TicketCommandGuard.ApplyRule(ticket, t => t.Unassign(now));

        return await TicketCommandGuard.SaveWithSystemAuditAsync(writer, ticket, actorId, "Yêu cầu đã được hủy phân công xử lý.", now, cancellationToken);
    }
}
