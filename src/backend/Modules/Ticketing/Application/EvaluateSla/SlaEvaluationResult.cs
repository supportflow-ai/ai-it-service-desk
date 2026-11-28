namespace ServiceDesk.Modules.Ticketing.Application.EvaluateSla;

/// <summary>
/// Kết quả tổng hợp của một lần sweep đánh giá SLA.
/// </summary>
public sealed record SlaEvaluationResult(
    int ProcessedCount,
    int UpdatedCount,
    int ConflictCount,
    int FailedCount = 0)
{
    public string Outcome => FailedCount > 0
        ? FailedCount == ProcessedCount ? "Failure" : "PartialFailure"
        : ProcessedCount == 0 ? "NoWork" : "Success";
}
