namespace ServiceDesk.Modules.Ticketing.Application.Contracts;

public record SlaPerformanceReportDto(
    int TotalTickets,
    int ResolvedTickets,
    int FirstResponseBreachedCount,
    double FirstResponseComplianceRate,
    int ResolutionBreachedCount,
    double ResolutionComplianceRate,
    double AverageMttaMinutes,
    double AverageMttrMinutes
);

public record AgentWorkloadDto(
    Guid AgentId,
    string? AgentName,
    int ActiveTicketCount,
    int ResolvedTicketCount
);

public record TicketDistributionReportDto(
    IReadOnlyDictionary<string, int> ByStatus,
    IReadOnlyDictionary<string, int> ByPriority,
    IReadOnlyDictionary<string, int> ByCategory,
    IReadOnlyList<AgentWorkloadDto> ByAssignee
);
