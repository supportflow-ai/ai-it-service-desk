using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Common;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.Ticketing.Application.TriageTicket;

public record TriageTicketCommand(Guid Id, Impact Impact, Urgency Urgency, uint? ExpectedRevision = null);

public interface ITriageTicket
{
    Task<TicketDto> ExecuteAsync(TriageTicketCommand command, CancellationToken cancellationToken = default);
}

internal sealed class TriageTicketUseCase(
    ITicketWriter writer,
    ICurrentUser currentUser,
    IClock clock) : ITriageTicket
{
    public async Task<TicketDto> ExecuteAsync(TriageTicketCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        if (!TicketPermissions.IsStaff(currentUser))
        {
            throw new UnauthorizedAccessException("Chỉ nhân viên hỗ trợ (Agent hoặc Admin) mới có quyền phân loại vé.");
        }

        ArgumentNullException.ThrowIfNull(command);

        var scope = TicketPermissions.ReadScopeFor(currentUser, actorId);
        var ticket = await writer.GetForUpdateAsync(command.Id, scope, cancellationToken);
        if (ticket == null)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        TicketCommandGuard.RequireCurrentRevision(writer, ticket, command.ExpectedRevision);

        var now = clock.UtcNow.UtcDateTime;
        TicketCommandGuard.ApplyRule(ticket, t => t.SetTriage(command.Impact, command.Urgency, now), includeArgumentErrors: true);

        return await TicketCommandGuard.SaveWithSystemAuditAsync(
            writer,
            ticket,
            actorId,
            $"Đã phân loại thủ công: Ảnh hưởng {command.Impact}, Khẩn cấp {command.Urgency}, Mức ưu tiên {ticket.Priority}.",
            now,
            cancellationToken);
    }
}
