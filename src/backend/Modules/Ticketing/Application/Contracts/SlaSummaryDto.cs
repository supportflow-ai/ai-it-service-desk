namespace ServiceDesk.Modules.Ticketing.Application.Contracts;

/// <summary>
/// Tóm tắt trạng thái của một đồng hồ cam kết SLA (First Response hoặc Resolution).
/// </summary>
public record SlaClockSummaryDto(
    int? TargetMinutes,
    double? ElapsedMinutes,
    double? RemainingMinutes,
    DateTime? Deadline,
    bool IsBreached,
    string State
);

/// <summary>
/// Tóm tắt SLA tổng thể của ticket trả về trong TicketDto.
/// </summary>
public record SlaSummaryDto(
    bool IsApplicable,
    DateTime EvaluatedAt,
    string OverallStatus,
    SlaClockSummaryDto? FirstResponse,
    SlaClockSummaryDto? Resolution
);
