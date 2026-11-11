using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Common;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.AssignTicket;

/// <summary>Lệnh phân công ticket cho kỹ thuật viên.</summary>
public record AssignTicketCommand(Guid Id, Guid AgentId, uint? ExpectedRevision = null);

/// <summary>Hợp đồng use case phân công vé.</summary>
public interface IAssignTicket
{
    Task<TicketDto> ExecuteAsync(AssignTicketCommand command, CancellationToken cancellationToken = default);
}

internal sealed class AssignTicketUseCase(
    ITicketWriter writer,
    IAgentDirectory agentDirectory,
    ICurrentUser currentUser,
    IClock clock) : IAssignTicket
{
    public async Task<TicketDto> ExecuteAsync(AssignTicketCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        var isStaff = TicketPermissions.IsStaff(currentUser);
        if (!isStaff)
        {
            throw TicketErrors.StaffRequired("phân công phiếu");
        }

        ArgumentNullException.ThrowIfNull(command);

        var scope = TicketPermissions.ReadScopeFor(currentUser, actorId);
        var ticket = await writer.GetForUpdateAsync(command.Id, scope, cancellationToken);
        if (ticket == null)
        {
            throw TicketErrors.NotFound();
        }

        if (!await agentDirectory.IsAgentAsync(command.AgentId, cancellationToken))
        {
            throw new InvalidInputException("ticket_assignee_invalid", "Kỹ thuật viên không tồn tại hoặc không có vai trò Agent.");
        }

        TicketCommandGuard.RequireCurrentRevision(writer, ticket, command.ExpectedRevision);

        var now = clock.UtcNow.UtcDateTime;
        ticket.Assign(command.AgentId, now);

        return await TicketCommandGuard.SaveWithSystemAuditAsync(writer, ticket, actorId, "Yêu cầu đã được phân công cho kỹ thuật viên.", now, cancellationToken);
    }
}
