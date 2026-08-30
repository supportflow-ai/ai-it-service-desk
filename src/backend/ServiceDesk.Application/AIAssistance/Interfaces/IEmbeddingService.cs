namespace ServiceDesk.Application.AIAssistance.Interfaces;

/// <summary>
/// Embedding service for vector-based similarity search.
/// Infrastructure provides NoOp implementation for bootstrap; real provider later.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generate an embedding vector for the given text.
    /// Returns empty array in NoOp mode.
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);
}
