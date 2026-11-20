using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Ticketing.Application.GetAgentQueue;

/// <summary>Truy vấn danh sách vé trong hàng đợi Agent.</summary>
public sealed record GetAgentQueueQuery(
    int? Status = null,
    string? CategoryId = null,
    string? Scope = null,
    int Page = 1,
    int PageSize = 10,
    string? Priority = null,
    bool SortByPriority = false,
    string? SlaStatus = null);

/// <summary>Hợp đồng use case lấy danh sách vé trong hàng đợi của Agent.</summary>
public interface IGetAgentQueue
{
    Task<PagedTicketResult> ExecuteAsync(GetAgentQueueQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý lấy danh sách vé trong hàng đợi Agent:
/// - Yêu cầu người gọi có vai trò Agent hoặc Admin.
/// - Kiểm tra ràng buộc phân trang (Page >= 1, 1 <= PageSize <= 100).
/// - Lọc theo trạng thái, danh mục, phân loại phạm vi (my/unassigned/all), hoặc SLA status (All/OnTrack/AtRisk/Breached).
/// </summary>
internal sealed class GetAgentQueueUseCase(ITicketQueries queries, ICurrentUser currentUser, IClock? clock = null) : IGetAgentQueue
{
    private static readonly HashSet<string> AllowedSlaStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "All", "OnTrack", "AtRisk", "Breached"
    };

    public Task<PagedTicketResult> ExecuteAsync(GetAgentQueueQuery query, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(query);

        var isStaff = TicketPermissions.IsStaff(currentUser);
        if (!isStaff)
        {
            throw new UnauthorizedAccessException("Only agents or admins can access the agent queue.");
        }

        if (query.Page < 1 || query.PageSize < 1 || query.PageSize > 100 || ((long)query.Page - 1) * query.PageSize > int.MaxValue)
        {
            throw new ArgumentException("page must be at least 1 and pageSize must be between 1 and 100");
        }

        if (!string.IsNullOrWhiteSpace(query.SlaStatus) && !AllowedSlaStatuses.Contains(query.SlaStatus))
        {
            throw new ArgumentException("slaStatus must be one of All, OnTrack, AtRisk, Breached");
        }

        var readScope = TicketPermissions.ReadScopeFor(currentUser, actorId);
        var now = clock?.UtcNow.UtcDateTime ?? DateTime.UtcNow;

        return queries.GetAgentQueueAsync(
            actorId,
            readScope,
            query.Status,
            query.CategoryId,
            query.Scope,
            query.Priority,
            query.SortByPriority,
            query.Page,
            query.PageSize,
            query.SlaStatus,
            now,
            cancellationToken);
    }
}
