using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
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
            ticket.Reopen(now);
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message);
        }

        var audit = TicketComment.CreatePublic(ticket.Id, actorId, "Hệ thống", "System", "Người dùng đã mở lại yêu cầu.", now);
        return await writer.UpdateTicketWithAuditAsync(ticket, audit, cancellationToken);
    }
}
