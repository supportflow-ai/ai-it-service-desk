using System.Security.Cryptography;
using System.Buffers.Binary;
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

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes("ai-input-v2\0"));
        Span<byte> length = stackalloc byte[4];
        foreach (var field in new[] { title.Trim(), (description ?? string.Empty).Trim() })
        {
            var bytes = Encoding.UTF8.GetBytes(field);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    public static bool IsValid(string? hash) => hash is { Length: 64 } &&
        hash.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
