using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Authorization;

namespace ServiceDesk.Modules.Ticketing.Application.Ports;

/// <summary>
/// Cổng truy vấn dữ liệu ticket do tầng Application làm chủ.
/// </summary>
public interface ITicketQueries
{
    /// <summary>Lấy danh sách vé có phân trang thuộc quyền sở hữu của một Requester.</summary>
    Task<PagedTicketResult> GetForRequesterAsync(Guid requesterId, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Lấy thông tin chi tiết một vé theo phạm vi quyền (TicketReadScope).</summary>
    Task<TicketDto?> GetByIdAsync(Guid id, TicketReadScope scope, CancellationToken cancellationToken);

    /// <summary>Truy vấn hàng đợi vé cho nhân viên hỗ trợ (Agent Queue) có lọc và phân trang.</summary>
    /// <param name="currentUserId">Người gọi; dùng cho phạm vi <see cref="AgentQueueScope.Mine"/>.</param>
    /// <param name="filter">Bộ lọc đã được Application kiểm tra; adapter chỉ áp dụng, không tự kiểm đầu vào.</param>
    Task<PagedTicketResult> GetAgentQueueAsync(Guid currentUserId, TicketReadScope readScope, AgentQueueFilter filter, int page, int pageSize, CancellationToken cancellationToken);
}
