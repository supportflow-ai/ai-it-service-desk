namespace ServiceDesk.Modules.Ticketing.Application.Contracts;

/// <summary>Khoảng thời gian thống kê đã được chuẩn hóa sang UTC.</summary>
public sealed record DateRangeDto(DateTime From, DateTime To);

/// <summary>Chỉ số tuân thủ SLA của một loại đồng hồ (First Response hoặc Resolution).</summary>
public sealed record SlaComplianceMetricDto(
    int Achieved,
    int Breached,
    double? Rate
);

/// <summary>Dữ liệu tổng hợp báo cáo phân tích hiệu suất và chất lượng dịch vụ.</summary>
public sealed record TicketAnalyticsDto(
    DateRangeDto Range,
    DateTime EvaluatedAt,
    int Volume,
    IReadOnlyDictionary<string, int> StatusDistribution,
    IReadOnlyDictionary<string, int> PriorityDistribution,
    SlaComplianceMetricDto FirstResponseCompliance,
    SlaComplianceMetricDto ResolutionCompliance,
    double? AverageResolutionMinutes,
    int ResolutionEligibleCount
);
