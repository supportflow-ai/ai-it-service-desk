namespace ServiceDesk.Modules.Ticketing.Application.EvaluateSla;

/// <summary>
/// Kết quả tổng hợp của một lần sweep đánh giá SLA.
/// </summary>
public sealed record SlaEvaluationResult(
    int ProcessedCount,
    int UpdatedCount,
    int ConflictCount);
