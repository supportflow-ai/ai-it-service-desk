using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Common;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;

namespace ServiceDesk.Modules.Ticketing.Application.AssignTicket;

/// <summary>Lệnh phân công ticket cho kỹ thuật viên.</summary>
public record AssignTicketCommand(Guid Id, Guid AgentId, uint? ExpectedRevision = null);

/// <summary>Hợp đồng use case phân công vé.</summary>
public interface IAssignTicket
{
    Task<TicketDto> ExecuteAsync(AssignTicketCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý phân công vé cho kỹ thuật viên (khung chung: <see cref="TicketTransitions"/>): chỉ Agent hoặc Admin;
/// người được phân công phải tồn tại và có vai trò Agent (kiểm trước khi kiểm revision); ghi log vào timeline.
/// </summary>
internal sealed class AssignTicketUseCase(
    ITicketWriter writer,
    IAgentDirectory agentDirectory,
    ICurrentUser currentUser,
    IClock clock) : IAssignTicket
{
    public async Task<TicketDto> ExecuteAsync(AssignTicketCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await new TicketTransitions(writer, currentUser, clock).RunAsStaffAsync(
            command.Id, command.ExpectedRevision, "phân công phiếu",
            apply: (ticket, _, now) => ticket.Assign(command.AgentId, now),
            auditMessage: "Yêu cầu đã được phân công cho kỹ thuật viên.",
            cancellationToken: cancellationToken,
            beforeRevisionCheck: async (_, _, token) =>
            {
                if (!await agentDirectory.IsAgentAsync(command.AgentId, token))
                {
                    throw new InvalidInputException("ticket_assignee_invalid", "Kỹ thuật viên không tồn tại hoặc không có vai trò Agent.");
                }
            });
    }
}
