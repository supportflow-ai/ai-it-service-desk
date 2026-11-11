using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Common;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;

namespace ServiceDesk.Modules.Ticketing.Application.UpdateTicket;

/// <summary>Lệnh cập nhật thông tin ticket (tiêu đề, mô tả, danh mục).</summary>
public record UpdateTicketCommand(Guid Id, string Title, string Description, string CategoryId, uint? ExpectedRevision = null);

/// <summary>Hợp đồng use case cập nhật ticket.</summary>
public interface IUpdateTicket
{
    Task<TicketDto> ExecuteAsync(UpdateTicketCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý cập nhật nội dung vé (chủ sở hữu hoặc Admin; chỉ khi Submitted). Dùng khung chung
/// <see cref="TicketTransitions"/> nhưng không ghi audit vào timeline.
/// </summary>
internal sealed class UpdateTicketUseCase(ITicketWriter writer, ICurrentUser currentUser, IClock clock) : IUpdateTicket
{
    public async Task<TicketDto> ExecuteAsync(UpdateTicketCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await new TicketTransitions(writer, currentUser, clock).RunAsOwnerAsync(
            command.Id, command.ExpectedRevision,
            apply: (ticket, now) => ticket.Update(command.Title, command.Description, command.CategoryId, now),
            auditMessage: null,
            cancellationToken: cancellationToken);
    }
}
