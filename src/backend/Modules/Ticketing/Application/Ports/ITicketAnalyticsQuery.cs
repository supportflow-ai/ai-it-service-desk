namespace ServiceDesk.Modules.Ticketing.Application.Ports;

/// <summary>
/// Kết quả thống kê tổng hợp từ cơ sở dữ liệu qua một snapshot transaction nhất quán.
/// </summary>
public sealed record TicketAnalyticsQueryResult(
    int Volume,
    IReadOnlyDictionary<string, int> StatusDistribution,
    IReadOnlyDictionary<string, int> PriorityDistribution,
    int FirstResponseAchieved,
    int FirstResponseBreached,
    int ResolutionAchieved,
    int ResolutionBreached,
    int ResolutionEligibleCount,
    double ResolutionTotalMinutes
);

/// <summary>
/// Cổng truy vấn tổng hợp phân tích dữ liệu ticket do tầng Application làm chủ.
/// </summary>
public interface ITicketAnalyticsQuery
{
    /// <summary>
    /// Thực thi truy vấn tổng hợp số liệu trong phạm vi thời gian và quyền hạn của actor.
    /// </summary>
    Task<TicketAnalyticsQueryResult> QueryAnalyticsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        DateTime evaluatedAt,
        bool isAdmin,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
