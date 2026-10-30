using ServiceDesk.Application.Common;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Ticketing.UpdateTicket;

/// <summary>Lệnh cập nhật thông tin ticket (tiêu đề, mô tả, danh mục).</summary>
public record UpdateTicketCommand(Guid Id, string Title, string Description, string CategoryId, uint? ExpectedRevision = null);

/// <summary>Hợp đồng use case cập nhật ticket.</summary>
public interface IUpdateTicket
{
    Task<TicketDto> ExecuteAsync(UpdateTicketCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý cập nhật thông tin ticket:
/// - Chỉ chủ sở hữu (Requester tạo vé) hoặc Admin mới có quyền thực thi.
/// - Trạng thái vé bắt buộc phải là Submitted (chưa qua xử lý).
/// - Xác thực tính hợp lệ của tiêu đề, mô tả và danh mục theo quy tắc Domain.
/// </summary>
internal sealed class UpdateTicketUseCase(ITicketWriter writer, ICurrentUser currentUser, IClock clock) : IUpdateTicket
{
    public async Task<TicketDto> ExecuteAsync(UpdateTicketCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(command);

        var ticket = await writer.GetForUpdateAsync(command.Id, cancellationToken);
        if (ticket == null || (!ticket.IsOwnedBy(actorId) && !currentUser.IsInRole(RoleNames.Admin)))
        {
            throw new KeyNotFoundException("Ticket not found or outside access scope.");
        }

        if (command.ExpectedRevision.HasValue && command.ExpectedRevision.Value > 0)
        {
            var currentRev = writer.GetRevision(ticket);
            if (currentRev != 0 && currentRev != command.ExpectedRevision.Value)
            {
                throw new ConcurrencyConflictException("Dữ liệu đã bị thay đổi bởi người khác hoặc phiên làm việc đã cũ. Vui lòng tải lại trang.");
            }
        }

        try
        {
            ticket.Update(command.Title, command.Description, command.CategoryId, clock.UtcNow.UtcDateTime);
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message);
        }

        return await writer.UpdateTicketAsync(ticket, cancellationToken);
    }
}
