using ServiceDesk.Application.AIAssistance.Interfaces;

namespace ServiceDesk.Infrastructure.AI;

/// <summary>
/// No-op embedding service — returns empty vector.
/// Placeholder until a real AI provider is integrated.
/// </summary>
public sealed class NoOpEmbeddingService : IEmbeddingService
{
    public Task<float[]> GenerateEmbeddingAsync(
        string text, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Array.Empty<float>());
    }
}
