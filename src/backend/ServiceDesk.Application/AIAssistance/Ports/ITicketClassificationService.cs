using ServiceDesk.Application.AIAssistance.Contracts;

namespace ServiceDesk.Application.AIAssistance.Ports;

/// <summary>
/// Cổng dịch vụ phân loại ticket bằng AI (gợi ý danh mục, mức độ ảnh hưởng, mức độ khẩn cấp).
/// Tầng Infrastructure cung cấp cài đặt NoOp khi chưa nối AI service thật.
/// </summary>
public interface ITicketClassificationService
{
    /// <summary>
    /// Phân loại ticket dựa trên dữ liệu đầu vào đã được tối giản (TriageRequest).
    /// Trả về đối tượng TriageResult có kiểm soát lỗi chặt chẽ, không throw exception khi AI gặp sự cố.
    /// </summary>
    Task<TriageResult> ClassifyAsync(
        TriageRequest request,
        CancellationToken cancellationToken = default);
}
