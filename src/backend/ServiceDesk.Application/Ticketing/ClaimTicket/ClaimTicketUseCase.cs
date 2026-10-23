using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Ticketing.ClaimTicket;

/// <summary>Lệnh yêu cầu tiếp nhận ticket (Claim).</summary>
public record ClaimTicketCommand(Guid Id);

/// <summary>Hợp đồng use case kỹ thuật viên nhận vé.</summary>
public interface IClaimTicket
{
    Task<TicketDto> ExecuteAsync(ClaimTicketCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý tiếp nhận vé:
/// - Yêu cầu người gọi có vai trò Agent hoặc Admin.
/// - Kiểm tra trạng thái vé phải là Submitted.
/// - Chuyển trạng thái sang InProgress và lưu lại thời điểm cập nhật.
/// </summary>
internal sealed class ClaimTicketUseCase(ITicketWriter writer, ICurrentUser currentUser, IClock clock) : IClaimTicket
{
    public async Task<TicketDto> ExecuteAsync(ClaimTicketCommand command, CancellationToken cancellationToken = default)
    {
        TicketActor.RequireId(currentUser);
        if (!currentUser.IsInRole(RoleNames.Agent) && !currentUser.IsInRole(RoleNames.Admin))
        {
            throw new UnauthorizedAccessException("Yêu cầu vai trò Agent hoặc Admin để nhận xử lý phiếu.");
        }

        ArgumentNullException.ThrowIfNull(command);

        var ticket = await writer.GetForUpdateAsync(command.Id, cancellationToken);
        if (ticket == null)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        try
        {
            ticket.Claim(clock.UtcNow.UtcDateTime);
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message);
        }

        return await writer.UpdateTicketAsync(ticket, cancellationToken);
    }
}
