using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Common;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Ticketing.Application.UpdateTicket;

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

        var scope = TicketPermissions.OwnerScopeFor(currentUser, actorId);
        var ticket = await writer.GetForUpdateAsync(command.Id, scope, cancellationToken);
        if (ticket == null || !TicketPermissions.CanActAsOwner(ticket, actorId, currentUser))
        {
            throw new KeyNotFoundException("Ticket not found or outside access scope.");
        }

        TicketCommandGuard.RequireCurrentRevision(writer, ticket, command.ExpectedRevision);

        TicketCommandGuard.ApplyRule(ticket, t => t.Update(command.Title, command.Description, command.CategoryId, clock.UtcNow.UtcDateTime));

        return await writer.UpdateTicketAsync(ticket, cancellationToken);
    }
}
