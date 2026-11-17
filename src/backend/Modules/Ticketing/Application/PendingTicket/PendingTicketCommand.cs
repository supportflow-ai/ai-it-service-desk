using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.PendingTicket;

/// <summary>Lệnh tạm dừng xử lý ticket sang PendingUser hoặc PendingExternal.</summary>
public record PendingTicketCommand(Guid Id, TicketStatus PendingStatus, uint? ExpectedRevision = null);

/// <summary>Hợp đồng use case tạm dừng xử lý ticket.</summary>
public interface IPendingTicket
{
    Task<TicketDto> ExecuteAsync(PendingTicketCommand command, CancellationToken cancellationToken = default);
}
