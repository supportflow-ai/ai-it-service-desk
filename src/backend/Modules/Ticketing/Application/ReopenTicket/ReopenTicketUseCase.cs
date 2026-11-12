using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Common;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;

namespace ServiceDesk.Modules.Ticketing.Application.ReopenTicket;

/// <summary>Lệnh mở lại ticket khi chưa đạt yêu cầu (Reopen).</summary>
public record ReopenTicketCommand(Guid Id, uint? ExpectedRevision = null);

/// <summary>Hợp đồng use case mở lại ticket.</summary>
public interface IReopenTicket
{
    Task<TicketDto> ExecuteAsync(ReopenTicketCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý mở lại vé, Resolved sang InProgress (chủ sở hữu hoặc Admin), và ghi log vào timeline.
/// Khung chung của lệnh chuyển trạng thái: <see cref="TicketTransitions"/>; phiếu không có hoặc không thuộc quyền trả 404.
/// </summary>
internal sealed class ReopenTicketUseCase(
    ITicketWriter writer,
    ICurrentUser currentUser,
    IClock clock) : IReopenTicket
{
    public async Task<TicketDto> ExecuteAsync(ReopenTicketCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await new TicketTransitions(writer, currentUser, clock).RunAsOwnerAsync(
            command.Id, command.ExpectedRevision,
            apply: (ticket, now) => ticket.Reopen(now),
            auditMessage: "Người dùng đã mở lại yêu cầu.",
            cancellationToken: cancellationToken);
    }
}
