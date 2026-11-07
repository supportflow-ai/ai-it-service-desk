using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
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

        var ticket = await writer.GetForUpdateAsync(command.Id, cancellationToken);
        if (ticket == null || (!ticket.IsOwnedBy(actorId) && !currentUser.IsInRole(RoleNames.Admin)))
        {
            throw new KeyNotFoundException("Ticket not found or outside access scope.");
        }

        if (!command.ExpectedRevision.HasValue || command.ExpectedRevision.Value == 0)
        {
            throw new ArgumentException("Expected revision is required.");
        }

        var currentRev = writer.GetRevision(ticket);
        if (currentRev != command.ExpectedRevision.Value)
        {
            throw new ConcurrencyConflictException("Dữ liệu đã bị thay đổi bởi người khác hoặc phiên làm việc đã cũ. Vui lòng tải lại trang.");
        }

        var now = clock.UtcNow.UtcDateTime;
        try
        {
            ticket.Close(now);
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message);
        }

        var audit = TicketComment.CreatePublic(ticket.Id, actorId, "Hệ thống", "System", "Yêu cầu đã được đóng hoàn tất sau nghiệm thu.", now);
        return await writer.UpdateTicketWithAuditAsync(ticket, audit, cancellationToken);
    }
}
