namespace ServiceDesk.Domain.Common;

/// <summary>
/// Marker class for Domain.Common namespace.
/// Domain entities and value objects will be added here in future sprints.
/// </summary>
public static class DomainDefaults
{
    /// <summary>
    /// Maximum length for standard name fields across the domain.
    /// </summary>
    public const int NameMaxLength = 256;

    /// <summary>
    /// Maximum length for description/content fields.
    /// </summary>
    public const int DescriptionMaxLength = 4000;
}
