using ServiceDesk.Modules.Ticketing.Application.Contracts;

namespace ServiceDesk.Modules.Ticketing.Application.GetTicketAnalytics;

/// <summary>Yêu cầu truy vấn báo cáo thống kê ticket theo khoảng thời gian.</summary>
public sealed record GetTicketAnalyticsQuery(string? From, string? To);

/// <summary>Hợp đồng use case truy vấn báo cáo phân tích hiệu suất và chất lượng dịch vụ.</summary>
public interface IGetTicketAnalytics
{
    Task<TicketAnalyticsDto> ExecuteAsync(GetTicketAnalyticsQuery query, CancellationToken cancellationToken = default);
}
