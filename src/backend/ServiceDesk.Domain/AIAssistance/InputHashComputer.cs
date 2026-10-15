using System.Security.Cryptography;
using System.Text;

namespace ServiceDesk.Domain.AIAssistance;

/// <summary>
/// Deterministic hash computation for AI-relevant ticket input.
/// Used for stale-suggestion detection: if the hash changes,
/// all pending suggestions based on the old input are stale.
///
/// Hash covers only the fields sent to the AI classifier (Title + Description).
/// Uses SHA-256 for stability and determinism.
/// </summary>
public static class InputHashComputer
{
    /// <summary>
    /// Compute a deterministic SHA-256 hash of the canonical AI input.
    /// </summary>
    /// <param name="title">Ticket title (required).</param>
    /// <param name="description">Ticket description (may be null/empty).</param>
    /// <returns>Hex-encoded SHA-256 hash string.</returns>
    public static string Compute(string title, string? description)
    {
        ArgumentNullException.ThrowIfNull(title);

        // Canonical format: normalized to lowercase, trimmed, with separator
        var canonical = $"TITLE:{title.Trim()}\nDESCRIPTION:{(description ?? string.Empty).Trim()}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
