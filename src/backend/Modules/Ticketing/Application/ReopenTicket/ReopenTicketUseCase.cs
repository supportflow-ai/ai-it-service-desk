using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Common;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.ReopenTicket;

/// <summary>Lệnh mở lại ticket khi chưa đạt yêu cầu (Reopen).</summary>
public record ReopenTicketCommand(Guid Id, uint? ExpectedRevision = null);

/// <summary>Hợp đồng use case mở lại ticket.</summary>
public interface IReopenTicket
{
    Task<TicketDto> ExecuteAsync(ReopenTicketCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý mở lại ticket:
/// - Chỉ chủ sở hữu (Requester tạo vé) hoặc Admin mới có quyền thực thi.
/// - Trạng thái vé bắt buộc phải là Resolved.
/// - Chuyển trạng thái quay trở lại InProgress để kỹ thuật viên tiếp tục xử lý.
/// </summary>
internal sealed class ReopenTicketUseCase(
    ITicketWriter writer,
    ICurrentUser currentUser,
    IClock clock) : IReopenTicket
{
    public async Task<TicketDto> ExecuteAsync(ReopenTicketCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(command);

        var scope = TicketPermissions.OwnerScopeFor(currentUser, actorId);
        var ticket = await writer.GetForUpdateAsync(command.Id, scope, cancellationToken);
        if (ticket == null || !TicketPermissions.CanActAsOwner(ticket, actorId, currentUser))
        {
            throw TicketErrors.NotFound();
        }

        TicketCommandGuard.RequireCurrentRevision(writer, ticket, command.ExpectedRevision);

        var now = clock.UtcNow.UtcDateTime;
        ticket.Reopen(now);

        return await TicketCommandGuard.SaveWithSystemAuditAsync(writer, ticket, actorId, "Người dùng đã mở lại yêu cầu.", now, cancellationToken);
    }
}
