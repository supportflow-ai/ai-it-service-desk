using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Common;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.ResolveTicket;

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
    ICurrentUser currentUser,
    IClock clock) : IResolveTicket
{
    public async Task<TicketDto> ExecuteAsync(ResolveTicketCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(command);
        var isStaff = TicketPermissions.IsStaff(currentUser);
        if (!isStaff)
        {
            throw new UnauthorizedAccessException("Only an agent or admin can resolve this ticket.");
        }

        var ticket = await writer.GetForUpdateAsync(command.Id, cancellationToken);
        if (ticket == null)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        TicketCommandGuard.RequireCurrentRevision(writer, ticket, command.ExpectedRevision);

        var now = clock.UtcNow.UtcDateTime;
        TicketCommandGuard.ApplyRule(ticket, t => t.Resolve(now));

        return await TicketCommandGuard.SaveWithSystemAuditAsync(writer, ticket, actorId, "Yêu cầu đã được đánh dấu giải quyết xong.", now, cancellationToken);
    }
}
