using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Common;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;

namespace ServiceDesk.Modules.Ticketing.Application.CloseTicket;

/// <summary>Lệnh đóng hoàn tất ticket sau khi nghiệm thu (Close).</summary>
public record CloseTicketCommand(Guid Id, uint? ExpectedRevision = null);

/// <summary>Hợp đồng use case đóng hoàn tất ticket.</summary>
public interface ICloseTicket
{
    Task<TicketDto> ExecuteAsync(CloseTicketCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý đóng vé sau nghiệm thu, Resolved sang Closed (chủ sở hữu hoặc Admin), và ghi log vào timeline.
/// Khung chung của lệnh chuyển trạng thái: <see cref="TicketTransitions"/>; phiếu không có hoặc không thuộc quyền trả 404.
/// </summary>
internal sealed class CloseTicketUseCase(
    ITicketWriter writer,
    ICurrentUser currentUser,
    IClock clock) : ICloseTicket
{
    public async Task<TicketDto> ExecuteAsync(CloseTicketCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await new TicketTransitions(writer, currentUser, clock).RunAsOwnerAsync(
            command.Id, command.ExpectedRevision,
            apply: (ticket, now) => ticket.Close(now),
            auditMessage: "Yêu cầu đã được đóng hoàn tất sau nghiệm thu.",
            cancellationToken: cancellationToken);
    }
}
