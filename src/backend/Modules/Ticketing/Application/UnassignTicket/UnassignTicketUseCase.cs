using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Common;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;

namespace ServiceDesk.Modules.Ticketing.Application.UnassignTicket;

/// <summary>Lệnh hủy phân công ticket.</summary>
public record UnassignTicketCommand(Guid Id, uint? ExpectedRevision = null);

/// <summary>Hợp đồng use case hủy phân công vé.</summary>
public interface IUnassignTicket
{
    Task<TicketDto> ExecuteAsync(UnassignTicketCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý hủy phân công vé (khung chung: <see cref="TicketTransitions"/>): chỉ Agent hoặc Admin;
/// vé phải đang Assigned và có người phụ trách; đưa vé về Submitted và ghi log vào timeline.
/// </summary>
internal sealed class UnassignTicketUseCase(
    ITicketWriter writer,
    ICurrentUser currentUser,
    IClock clock) : IUnassignTicket
{
    public async Task<TicketDto> ExecuteAsync(UnassignTicketCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await new TicketTransitions(writer, currentUser, clock).RunAsStaffAsync(
            command.Id, command.ExpectedRevision, "hủy phân công phiếu",
            apply: (ticket, _, now) => ticket.Unassign(now),
            auditMessage: "Yêu cầu đã được hủy phân công xử lý.",
            cancellationToken: cancellationToken);
    }
}
