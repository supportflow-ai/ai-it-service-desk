namespace ServiceDesk.Domain.AIAssistance;

/// <summary>
/// Type of AI suggestion within a triage analysis.
/// Each TriageResult may produce multiple suggestions — one per type.
/// </summary>
public enum SuggestionType
{
    Category,
    Impact,
    Urgency
}
