using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Common;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.CancelTicket;

/// <summary>Lệnh hủy ticket khi chưa xử lý (Cancel).</summary>
public record CancelTicketCommand(Guid Id, uint? ExpectedRevision = null);

/// <summary>Hợp đồng use case hủy ticket.</summary>
public interface ICancelTicket
{
    Task<TicketDto> ExecuteAsync(CancelTicketCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý hủy ticket:
/// - Chỉ chủ sở hữu (Requester tạo vé) hoặc Admin mới có quyền thực thi.
/// - Trạng thái vé bắt buộc phải là Submitted (chưa được kỹ thuật viên nhận xử lý).
/// - Chuyển trạng thái sang Cancelled (trạng thái kết thúc - terminal state, không thể sửa đổi tiếp).
/// </summary>
internal sealed class CancelTicketUseCase(
    ITicketWriter writer,
    ICurrentUser currentUser,
    IClock clock) : ICancelTicket
{
    public async Task<TicketDto> ExecuteAsync(CancelTicketCommand command, CancellationToken cancellationToken = default)
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
        TicketCommandGuard.ApplyRule(ticket, t => t.Cancel(now));

        return await TicketCommandGuard.SaveWithSystemAuditAsync(writer, ticket, actorId, "Yêu cầu đã được hủy bỏ.", now, cancellationToken);
    }
}
