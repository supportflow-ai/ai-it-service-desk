using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Authorization;

namespace ServiceDesk.Application.Ticketing.Ports;

/// <summary>
/// Cổng truy vấn dữ liệu ticket do tầng Application làm chủ.
/// </summary>
public interface ITicketQueries
{
    /// <summary>Lấy danh sách vé có phân trang thuộc quyền sở hữu của một Requester.</summary>
    Task<PagedTicketResult> GetForRequesterAsync(Guid requesterId, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Lấy thông tin chi tiết một vé theo phạm vi quyền (TicketReadScope).</summary>
    Task<TicketDto?> GetByIdAsync(Guid id, TicketReadScope scope, CancellationToken cancellationToken);
}
