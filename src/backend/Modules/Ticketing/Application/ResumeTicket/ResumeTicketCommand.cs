using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.ResumeTicket;

/// <summary>Lệnh tiếp tục xử lý ticket từ PendingUser hoặc PendingExternal trở lại InProgress.</summary>
public record ResumeTicketCommand(Guid Id, uint? ExpectedRevision = null);

/// <summary>Hợp đồng use case tiếp tục xử lý ticket.</summary>
public interface IResumeTicket
{
    Task<TicketDto> ExecuteAsync(ResumeTicketCommand command, CancellationToken cancellationToken = default);
}
