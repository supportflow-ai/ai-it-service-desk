using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Ticketing.GetAgentQueue;

/// <summary>Truy vấn danh sách vé trong hàng đợi Agent.</summary>
public sealed record GetAgentQueueQuery(int? Status = null, string? CategoryId = null, string? Scope = null, int Page = 1, int PageSize = 10);

/// <summary>Hợp đồng use case lấy danh sách vé trong hàng đợi của Agent.</summary>
public interface IGetAgentQueue
{
    Task<PagedTicketResult> ExecuteAsync(GetAgentQueueQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý lấy danh sách vé trong hàng đợi Agent:
/// - Yêu cầu người gọi có vai trò Agent hoặc Admin.
/// - Kiểm tra ràng buộc phân trang (Page >= 1, 1 <= PageSize <= 100).
/// - Lọc theo trạng thái, danh mục, hoặc phân loại phạm vi (my/unassigned/all).
/// </summary>
internal sealed class GetAgentQueueUseCase(ITicketQueries queries, ICurrentUser currentUser) : IGetAgentQueue
{
    public Task<PagedTicketResult> ExecuteAsync(GetAgentQueueQuery query, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(query);

        var isStaff = currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin);
        if (!isStaff)
        {
            throw new UnauthorizedAccessException("Only agents or admins can access the agent queue.");
        }

        if (query.Page < 1 || query.PageSize < 1 || query.PageSize > 100 || ((long)query.Page - 1) * query.PageSize > int.MaxValue)
        {
            throw new ArgumentException("page must be at least 1 and pageSize must be between 1 and 100");
        }

        return queries.GetAgentQueueAsync(actorId, query.Status, query.CategoryId, query.Scope, query.Page, query.PageSize, cancellationToken);
    }
}
