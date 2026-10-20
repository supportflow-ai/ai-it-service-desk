using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using ServiceDesk.Application.Ticketing.CreateTicket;

namespace ServiceDesk.Application.Ticketing.Idempotency;

/// <summary>
/// Đại diện cho dấu vân tay định danh (Fingerprint) của request tạo vé.
/// Dùng cho cơ chế Idempotency chống gửi lặp: khóa và payload gốc không bao giờ lưu dưới dạng thô.
/// </summary>
public sealed record CreateTicketFingerprint(string KeyHash, string PayloadHash, long LockId)
{
    public const string Method = "POST";
    public const string Resource = "/api/tickets";
    public const int MaximumKeyLength = 200;

    /// <summary>
    /// Tạo đối tượng CreateTicketFingerprint tất định từ Idempotency-Key và nội dung request.
    /// </summary>
    public static CreateTicketFingerprint Create(Guid requesterId, string key, CreateTicketCommand command)
    {
        ArgumentNullException.ThrowIfNull(key);
        var normalizedKey = key.Trim();
        if (normalizedKey.Length is 0 or > MaximumKeyLength ||
            normalizedKey.Any(character => character < '!' || character > '~'))
        {
            throw new ArgumentException("Idempotency-Key phải chứa từ 1 đến 200 ký tự ASCII hiển thị được.");
        }

        var keyHash = Hash([normalizedKey]);
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
