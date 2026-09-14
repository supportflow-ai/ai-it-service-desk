namespace ServiceDesk.Domain.AIAssistance;

/// <summary>
/// Human decision on an individual AI suggestion.
/// Lifecycle: Pending is the initial state.
/// Accepted, Overridden, Rejected, Expired are terminal — no further transitions.
///
/// Decision rules:
/// - Accepted:   FinalValue = SuggestedValue.
/// - Overridden: FinalValue must be a valid human-provided value.
/// - Rejected:   No AI value is applied.
/// - Expired:    Ticket changed since analysis — suggestion is stale and cannot be applied.
/// </summary>
public enum SuggestionDecision
{
    Pending,
    Accepted,
    Overridden,
    Rejected,
    Expired
}
