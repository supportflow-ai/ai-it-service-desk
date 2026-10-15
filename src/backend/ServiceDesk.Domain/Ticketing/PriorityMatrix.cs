namespace ServiceDesk.Domain.Ticketing;

/// <summary>
/// Deterministic priority calculation from Impact × Urgency.
/// This is a pure domain business rule — no AI involvement.
/// 
/// Matrix:
///              Urgency
///              High    Medium  Low
/// Impact High   P1      P2     P3
///        Med    P2      P3     P4
///        Low    P3      P4     P4
/// </summary>
public static class PriorityMatrix
{
    /// <summary>
    /// Calculate priority from impact and urgency using the standard 3×3 matrix.
    /// </summary>
    public static Priority Calculate(Impact impact, Urgency urgency)
    {
        return (impact, urgency) switch
        {
            (Impact.High, Urgency.High) => Priority.P1,
            (Impact.High, Urgency.Medium) => Priority.P2,
            (Impact.High, Urgency.Low) => Priority.P3,

            (Impact.Medium, Urgency.High) => Priority.P2,
            (Impact.Medium, Urgency.Medium) => Priority.P3,
            (Impact.Medium, Urgency.Low) => Priority.P4,

            (Impact.Low, Urgency.High) => Priority.P3,
            (Impact.Low, Urgency.Medium) => Priority.P4,
            (Impact.Low, Urgency.Low) => Priority.P4,

            _ => throw new ArgumentOutOfRangeException(
                $"Invalid Impact ({impact}) or Urgency ({urgency}) value.")
        };
    }
}
