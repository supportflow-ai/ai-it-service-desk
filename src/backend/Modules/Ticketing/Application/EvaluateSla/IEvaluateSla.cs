namespace ServiceDesk.Modules.Ticketing.Application.EvaluateSla;

/// <summary>
/// Hợp đồng use case định kỳ quét và đánh giá vi phạm SLA cho các ticket đang hoạt động.
/// </summary>
public interface IEvaluateSla
{
    Task<SlaEvaluationResult> ExecuteAsync(CancellationToken cancellationToken = default);
}
