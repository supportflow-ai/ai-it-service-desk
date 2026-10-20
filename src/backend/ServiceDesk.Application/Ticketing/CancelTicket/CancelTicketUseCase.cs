using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Ticketing.CancelTicket;

/// <summary>Lệnh hủy ticket khi chưa xử lý (Cancel).</summary>
public record CancelTicketCommand(Guid Id);

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
internal sealed class CancelTicketUseCase(ITicketWriter writer, ICurrentUser currentUser, IClock clock) : ICancelTicket
{
    public async Task<TicketDto> ExecuteAsync(CancelTicketCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(command);

        var ticket = await writer.GetForUpdateAsync(command.Id, cancellationToken);
        if (ticket == null || (!ticket.IsOwnedBy(actorId) && !currentUser.IsInRole(RoleNames.Admin)))
        {
            throw new KeyNotFoundException("Ticket not found or outside access scope.");
        }

        try
        {
            ticket.Cancel(clock.UtcNow.UtcDateTime);
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message);
        }

        return await writer.UpdateTicketAsync(ticket, cancellationToken);
    }
}
