namespace ServiceDesk.Infrastructure.Ticketing;

/// <summary>Infrastructure-only replay receipt, committed atomically with its ticket.</summary>
public sealed class TicketIdempotencyRecord
{
    public Guid Id { get; set; }
    public Guid RequesterId { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public Guid TicketId { get; set; }
    // Only the accepted response DTO is retained, to replay the original result exactly.
    // No Authorization header, token, raw key, or raw request body is persisted.
    public string ResponseJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
