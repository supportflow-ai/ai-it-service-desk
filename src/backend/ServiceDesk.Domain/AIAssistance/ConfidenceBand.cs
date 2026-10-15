namespace ServiceDesk.Domain.AIAssistance;

/// <summary>
/// Product-facing confidence band derived from raw model score.
/// Thresholds are defined by <see cref="ConfidencePolicy"/> — not hardcoded.
///
/// IMPORTANT: This is NOT a calibrated probability.
/// It is a discretized label derived from the raw model score to help
/// reviewers prioritize which suggestions to examine.
/// </summary>
public enum ConfidenceBand
{
    /// <summary>Model score is below threshold — suggestion needs careful review.</summary>
    Low,

    /// <summary>Model score is in mid-range — review recommended.</summary>
    Medium,

    /// <summary>Model score is above high threshold — likely accurate but human decides.</summary>
    High
}
