using ServiceDesk.Modules.AIAssistance.Application.Ports;

namespace ServiceDesk.Modules.AIAssistance.Infrastructure;

/// <summary>
/// Cài đặt NoOp cho dịch vụ Embedding — trả về vector rỗng.
/// Đóng vai trò giữ chỗ (placeholder) khi chưa tích hợp AI service thật.
/// </summary>
internal sealed class NoOpEmbeddingService : IEmbeddingService
{
    public Task<float[]> GenerateEmbeddingAsync(
        string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Array.Empty<float>());
    }
}
