namespace ServiceDesk.Application.AIAssistance.Ports;

/// <summary>
/// Cổng dịch vụ tạo vector nhúng (Embedding) phục vụ tìm kiếm tương đồng và RAG.
/// Tầng Infrastructure cung cấp cài đặt NoOp trong giai đoạn khởi tạo ban đầu.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Sinh vector nhúng cho một đoạn văn bản.
    /// Trả về mảng rỗng khi ở chế độ NoOp.
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);
}
