using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Common;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.CloseTicket;

/// <summary>Lệnh đóng hoàn tất ticket sau khi nghiệm thu (Close).</summary>
public record CloseTicketCommand(Guid Id, uint? ExpectedRevision = null);

/// <summary>Hợp đồng use case đóng hoàn tất ticket.</summary>
public interface ICloseTicket
{
    Task<TicketDto> ExecuteAsync(CloseTicketCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý đóng hoàn tất ticket:
/// - Chỉ chủ sở hữu (Requester tạo vé) hoặc Admin mới có quyền thực thi.
/// - Trạng thái vé bắt buộc phải là Resolved.
/// - Chuyển trạng thái sang Closed (trạng thái kết thúc - terminal state, không thể sửa đổi tiếp).
/// </summary>
internal sealed class CloseTicketUseCase(
    ITicketWriter writer,
    ICurrentUser currentUser,
    IClock clock) : ICloseTicket
{
    public async Task<TicketDto> ExecuteAsync(CloseTicketCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(command);

        var scope = TicketPermissions.OwnerScopeFor(currentUser, actorId);
        var ticket = await writer.GetForUpdateAsync(command.Id, scope, cancellationToken);
        if (ticket == null || !TicketPermissions.CanActAsOwner(ticket, actorId, currentUser))
        {
            throw new KeyNotFoundException("Ticket not found or outside access scope.");
        }

        TicketCommandGuard.RequireCurrentRevision(writer, ticket, command.ExpectedRevision);

        var now = clock.UtcNow.UtcDateTime;
        TicketCommandGuard.ApplyRule(ticket, t => t.Close(now));

        return await TicketCommandGuard.SaveWithSystemAuditAsync(writer, ticket, actorId, "Yêu cầu đã được đóng hoàn tất sau nghiệm thu.", now, cancellationToken);
    }
}
