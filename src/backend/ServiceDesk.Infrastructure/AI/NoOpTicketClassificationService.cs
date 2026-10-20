using ServiceDesk.Application.AIAssistance.Ports;
using ServiceDesk.Application.AIAssistance.Contracts;

namespace ServiceDesk.Infrastructure.AI;

/// <summary>
/// Cài đặt NoOp cho dịch vụ phân loại ticket — trả về kết quả Unavailable có kiểm soát.
/// Đóng vai trò giữ chỗ (placeholder) khi chưa tích hợp AI service thật.
///
/// Tuân thủ hợp đồng:
/// - Không throw exception khi không có AI.
/// - Trả về TriageResult đầy đủ với trạng thái Unavailable.
/// - Không giả lập điểm số tin cậy (không fake confidence score).
/// - Tôn trọng CancellationToken từ caller.
/// </summary>
internal sealed class NoOpTicketClassificationService : ITicketClassificationService
{
    public Task<TriageResult> ClassifyAsync(
        TriageRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            TriageResult.Unavailable("Chưa cấu hình AI provider. Sử dụng NoOp fallback."));
    }
}
