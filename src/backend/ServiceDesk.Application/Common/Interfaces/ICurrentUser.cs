namespace ServiceDesk.Application.Common.Interfaces;

/// <summary>
/// Provides information about the currently authenticated user.
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// The authenticated user's ID, or null if not authenticated.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Whether the current request is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// The roles assigned to the current user.
    /// </summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>
    /// Checks whether the user has the specified role.
    /// </summary>
    bool IsInRole(string role);
}
