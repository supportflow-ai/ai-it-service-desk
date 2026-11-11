using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Common;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;

namespace ServiceDesk.Modules.Ticketing.Application.ResolveTicket;

/// <summary>Lệnh hoàn thành xử lý ticket (Resolve).</summary>
public record ResolveTicketCommand(Guid Id, uint? ExpectedRevision = null);

/// <summary>Hợp đồng use case đánh dấu giải quyết vé.</summary>
public interface IResolveTicket
{
    Task<TicketDto> ExecuteAsync(ResolveTicketCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý đánh dấu hoàn thành vé (khung chung: <see cref="TicketTransitions"/>):
/// - Yêu cầu người gọi có vai trò Agent hoặc Admin.
/// - Trạng thái vé bắt buộc phải là InProgress.
/// - Chuyển trạng thái sang Resolved và ghi log vào timeline.
/// </summary>
internal sealed class ResolveTicketUseCase(
    ITicketWriter writer,
    ICurrentUser currentUser,
    IClock clock) : IResolveTicket
{
    public async Task<TicketDto> ExecuteAsync(ResolveTicketCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await new TicketTransitions(writer, currentUser, clock).RunAsStaffAsync(
            command.Id, command.ExpectedRevision, "giải quyết phiếu",
            apply: (ticket, _, now) => ticket.Resolve(now),
            auditMessage: "Yêu cầu đã được đánh dấu giải quyết xong.",
            cancellationToken: cancellationToken);
    }
}
