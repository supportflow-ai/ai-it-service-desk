using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.GetAgentQueue;

/// <summary>Truy vấn danh sách vé trong hàng đợi Agent.</summary>
public sealed record GetAgentQueueQuery(int? Status = null, string? CategoryId = null, string? Scope = null, int Page = 1, int PageSize = 10, string? Priority = null, bool SortByPriority = false);

/// <summary>Hợp đồng use case lấy danh sách vé trong hàng đợi của Agent.</summary>
public interface IGetAgentQueue
{
    Task<PagedTicketResult> ExecuteAsync(GetAgentQueueQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý lấy danh sách vé trong hàng đợi Agent:
/// - Yêu cầu người gọi có vai trò Agent hoặc Admin.
/// - Kiểm tra ràng buộc phân trang (Page >= 1, 1 <= PageSize <= 100).
/// - Dựng bộ lọc hợp lệ (<see cref="AgentQueueFilter"/>): trạng thái, danh mục, ưu tiên, phạm vi (my/unassigned/all).
/// </summary>
internal sealed class GetAgentQueueUseCase(ITicketQueries queries, ICurrentUser currentUser) : IGetAgentQueue
{
    public Task<PagedTicketResult> ExecuteAsync(GetAgentQueueQuery query, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(query);

        var isStaff = TicketPermissions.IsStaff(currentUser);
        if (!isStaff)
        {
            throw TicketErrors.StaffRequired("truy cập hàng đợi phiếu");
        }

        if (query.Page < 1 || query.PageSize < 1 || query.PageSize > 100 || ((long)query.Page - 1) * query.PageSize > int.MaxValue)
        {
            throw TicketErrors.PagingInvalid();
        }

        // Kiểm priority ở đây (sau kiểm phân trang, đúng thứ tự cũ); adapter không còn tự kiểm đầu vào.
        var filter = AgentQueueFilter.FromRaw(query.Status, query.CategoryId, query.Scope, query.Priority, query.SortByPriority);

        var readScope = TicketPermissions.ReadScopeFor(currentUser, actorId);
        return queries.GetAgentQueueAsync(actorId, readScope, filter, query.Page, query.PageSize, cancellationToken);
    }
}
