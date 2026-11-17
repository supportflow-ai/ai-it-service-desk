using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.Ticketing.Application.EvaluateSla;

/// <summary>
/// Xử lý đánh giá vi phạm cam kết SLA cho các ticket:
/// - Quét qua các batch bằng Keyset pagination ổn định.
/// - Đánh giá từng đồng hồ tại thời điểm IClock.UtcNow hiện tại.
/// - Chỉ cập nhật khi trạng thái vi phạm thay đổi từ false sang true.
/// - Không để entity conflict làm hỏng quá trình xử lý các ticket tiếp theo.
/// </summary>
internal sealed class EvaluateSlaUseCase(
    ISlaEvaluationGateway gateway,
    IClock clock,
    int batchSize = EvaluateSlaUseCase.DefaultBatchSize) : IEvaluateSla
{
    public const int DefaultBatchSize = 100;

    public async Task<SlaEvaluationResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow.UtcDateTime;
        Guid? cursor = null;
        int processed = 0;
        int updated = 0;
        int conflicts = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await gateway.GetActiveSlaTicketsBatchAsync(cursor, batchSize, cancellationToken);
            if (batch.Count == 0) break;

            foreach (var ticket in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();
                cursor = ticket.Id;
                processed++;

                var changed = ticket.EvaluateSlaBreach(now);
                if (changed)
                {
                    var success = await gateway.UpdateBreachedMarkersAsync(ticket, cancellationToken);
                    if (success)
                    {
                        updated++;
                    }
                    else
                    {
                        conflicts++;
                    }
                }
            }

            if (batch.Count < batchSize) break;
        }

        return new SlaEvaluationResult(processed, updated, conflicts);
    }
}
