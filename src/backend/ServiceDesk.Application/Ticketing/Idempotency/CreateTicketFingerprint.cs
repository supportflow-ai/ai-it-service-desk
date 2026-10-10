using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using ServiceDesk.Application.Ticketing.Commands;

namespace ServiceDesk.Application.Ticketing.Idempotency;

/// <summary>Versioned, unambiguous fingerprints; raw keys and payloads are never stored here.</summary>
public sealed record CreateTicketFingerprint(string KeyHash, string PayloadHash, long LockId)
{
    public const string Method = "POST";
    public const string Resource = "/api/tickets";
    public const int MaximumKeyLength = 200;

    public static CreateTicketFingerprint Create(Guid requesterId, string key, CreateTicketCommand command)
    {
        ArgumentNullException.ThrowIfNull(key);
        var normalizedKey = key.Trim();
        if (normalizedKey.Length is 0 or > MaximumKeyLength ||
            normalizedKey.Any(character => character < '!' || character > '~'))
        {
            throw new ArgumentException("Idempotency-Key must contain 1 to 200 visible ASCII characters.");
        }

        var keyHash = Hash([normalizedKey]);
        // Match the existing CreateTicket normalization; JSON preserves field boundaries.
        var payloadHash = Hash(["create-ticket-v1", command.Title.Trim(), command.Description.Trim(), command.CategoryId]);
        var scope = JsonSerializer.SerializeToUtf8Bytes(new[]
        {
            "ticket-idempotency-v1", requesterId.ToString("D"), Method, Resource, keyHash
        });
        var lockId = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(scope));
        return new CreateTicketFingerprint(keyHash, payloadHash, lockId);
    }

    private static string Hash(string[] values) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(values)));
}
