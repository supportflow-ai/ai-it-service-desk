using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Common;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;

namespace ServiceDesk.Modules.Ticketing.Application.CancelTicket;

/// <summary>Lệnh hủy ticket khi chưa xử lý (Cancel).</summary>
public record CancelTicketCommand(Guid Id, uint? ExpectedRevision = null);

/// <summary>Hợp đồng use case hủy ticket.</summary>
public interface ICancelTicket
{
    Task<TicketDto> ExecuteAsync(CancelTicketCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý hủy vé khi còn Submitted (chủ sở hữu hoặc Admin) và ghi log vào timeline.
/// Khung chung của lệnh chuyển trạng thái: <see cref="TicketTransitions"/>; phiếu không có hoặc không thuộc quyền trả 404.
/// </summary>
internal sealed class CancelTicketUseCase(
    ITicketWriter writer,
    ICurrentUser currentUser,
    IClock clock) : ICancelTicket
{
    public async Task<TicketDto> ExecuteAsync(CancelTicketCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await new TicketTransitions(writer, currentUser, clock).RunAsOwnerAsync(
            command.Id, command.ExpectedRevision,
            apply: (ticket, now) => ticket.Cancel(now),
            auditMessage: "Yêu cầu đã được hủy bỏ.",
            cancellationToken: cancellationToken);
    }
}
