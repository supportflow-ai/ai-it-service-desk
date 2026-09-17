namespace ServiceDesk.Domain.AIAssistance;

/// <summary>
/// Domain policy for confidence score interpretation.
/// Raw model scores are mapped to <see cref="ConfidenceBand"/> for product display.
///
/// Thresholds are centralized here — not scattered across frontend/backend.
/// Values can be made configurable via IOptions in the future.
///
/// IMPORTANT: These thresholds are NOT calibrated probabilities.
/// They are operational cutoffs for reviewer prioritization.
/// </summary>
public static class ConfidencePolicy
{
    /// <summary>Valid model score range: [0.0, 1.0].</summary>
    public const double MinScore = 0.0;

    /// <summary>Valid model score range: [0.0, 1.0].</summary>
    public const double MaxScore = 1.0;

    /// <summary>Scores below this are Low confidence.</summary>
    public const double LowThreshold = 0.4;

    /// <summary>Scores at or above this are High confidence.</summary>
    public const double HighThreshold = 0.75;

    /// <summary>
    /// Classify a raw model score into a confidence band.
    /// Returns null for invalid scores (NaN, Infinity, out-of-range).
    /// </summary>
    public static ConfidenceBand? Classify(double? score)
    {
        if (score is null)
            return null;

        if (!IsValidScore(score.Value))
            return null;

        return score.Value switch
        {
            < LowThreshold => ConfidenceBand.Low,
            >= HighThreshold => ConfidenceBand.High,
            _ => ConfidenceBand.Medium
        };
    }

    /// <summary>
    /// Validate that a score is within the acceptable range and not NaN/Infinity.
    /// </summary>
    public static bool IsValidScore(double score)
    {
        if (double.IsNaN(score) || double.IsInfinity(score))
            return false;

        return score >= MinScore && score <= MaxScore;
    }
}
