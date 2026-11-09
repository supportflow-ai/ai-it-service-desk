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
            throw new UnauthorizedAccessException("Only agents or admins can assign tickets.");
        }

        ArgumentNullException.ThrowIfNull(command);

        var scope = TicketPermissions.ReadScopeFor(currentUser, actorId);
        var ticket = await writer.GetForUpdateAsync(command.Id, scope, cancellationToken);
        if (ticket == null)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        if (!await agentDirectory.IsAgentAsync(command.AgentId, cancellationToken))
        {
            throw new ArgumentException("Kỹ thuật viên không tồn tại hoặc không có vai trò Agent.");
        }

        TicketCommandGuard.RequireCurrentRevision(writer, ticket, command.ExpectedRevision);

        var now = clock.UtcNow.UtcDateTime;
        TicketCommandGuard.ApplyRule(ticket, t => t.Assign(command.AgentId, now), includeArgumentErrors: true);

        return await TicketCommandGuard.SaveWithSystemAuditAsync(writer, ticket, actorId, "Yêu cầu đã được phân công cho kỹ thuật viên.", now, cancellationToken);
    }
}
