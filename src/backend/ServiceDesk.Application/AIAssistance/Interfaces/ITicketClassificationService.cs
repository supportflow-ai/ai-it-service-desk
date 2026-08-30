namespace ServiceDesk.Application.AIAssistance.Interfaces;

/// <summary>
/// AI-powered ticket classification (category, impact, urgency suggestion).
/// Infrastructure provides NoOp implementation for bootstrap; real provider later.
/// </summary>
public interface ITicketClassificationService
{
    /// <summary>
    /// Classify a ticket based on its title and description.
    /// Returns empty result in NoOp mode.
    /// </summary>
    Task<TicketClassificationResult> ClassifyAsync(
        string title,
        string description,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of AI ticket classification.
/// </summary>
public sealed record TicketClassificationResult
{
    public string? SuggestedCategory { get; init; }
    public string? SuggestedImpact { get; init; }
    public string? SuggestedUrgency { get; init; }
    public double Confidence { get; init; }

    public static TicketClassificationResult Empty => new()
    {
        Confidence = 0
    };
}
