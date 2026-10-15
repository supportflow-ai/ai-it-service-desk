using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Domain.AIAssistance;

/// <summary>
/// Domain policy for triage guardrails.
/// Enforces business rules about what AI can and cannot do.
///
/// These rules are enforced at the domain/application layer — not just frontend.
/// </summary>
public static class TriagePolicy
{
    /// <summary>
    /// Categories that always require manual human triage.
    /// AI suggestions for these categories are informational only — never auto-applied.
    /// </summary>
    private static readonly HashSet<TicketCategory> ManualTriageCategories = [TicketCategory.SEC];

    /// <summary>
    /// Determines if the given category requires mandatory manual triage.
    /// </summary>
    public static bool RequiresManualTriage(TicketCategory category)
        => ManualTriageCategories.Contains(category);

    /// <summary>
    /// Validates whether a suggested category code string maps to a known category.
    /// </summary>
    public static bool IsKnownCategory(string? categoryCode)
    {
        if (string.IsNullOrWhiteSpace(categoryCode))
            return false;

        return Enum.TryParse<TicketCategory>(categoryCode, ignoreCase: true, out _);
    }

    /// <summary>
    /// Attempts to parse a category code string to the domain enum.
    /// Returns null if the code is unknown.
    /// </summary>
    public static TicketCategory? ParseCategory(string? categoryCode)
    {
        if (string.IsNullOrWhiteSpace(categoryCode))
            return null;

        return Enum.TryParse<TicketCategory>(categoryCode, ignoreCase: true, out var result)
            ? result
            : null;
    }

    /// <summary>
    /// Attempts to parse an impact string to the domain enum.
    /// </summary>
    public static Impact? ParseImpact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Enum.TryParse<Impact>(value, ignoreCase: true, out var result) ? result : null;
    }

    /// <summary>
    /// Attempts to parse an urgency string to the domain enum.
    /// </summary>
    public static Urgency? ParseUrgency(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Enum.TryParse<Urgency>(value, ignoreCase: true, out var result) ? result : null;
    }

    /// <summary>
    /// AI is explicitly forbidden from performing these actions.
    /// This list serves as documentation and can be used for runtime guard checks.
    /// </summary>
    public static class ForbiddenAIActions
    {
        public const string AutoResolve = "AI must not auto-resolve tickets.";
        public const string AutoClose = "AI must not auto-close tickets.";
        public const string DeleteResource = "AI must not delete business resources.";
        public const string ResetPassword = "AI must not reset passwords.";
        public const string GrantPermission = "AI must not grant permissions.";
        public const string DeactivateUser = "AI must not deactivate users.";
        public const string PublishKB = "AI must not publish knowledge base articles.";
        public const string ChangeSLA = "AI must not change SLA configurations.";
        public const string TransitionTicketState = "AI must not transition ticket workflow state.";
        public const string SetFinalPriority = "AI must not set or determine final ticket priority.";
    }
}
